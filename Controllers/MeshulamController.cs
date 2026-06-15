using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Attributes;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using MyProject12;
using Umbraco.Cms.Web.Website.Controllers;
using GoogleReCaptcha.V3.Interface;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using MyProject12.Models;
using Microsoft.IdentityModel.Tokens;
using MyProject12.ViewModels;
using Umbraco.Cms.Web.Common.Security;
using Newtonsoft.Json.Linq;
using Amazon.SimpleEmail.Model.Internal.MarshallTransformations;
using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core;
using Umbraco.Cms.Web;
using System.Web;
using Umbraco.Cms.Infrastructure;
using System.Runtime.CompilerServices;
using MyProject12.Services;
using Umbraco.Cms.Web.Common.Filters;
using System.Text;
using System.Globalization;
using StackExchange.Profiling.Data;
using Microsoft.AspNetCore.Identity;
using MO6.Models;
using MO6.ViewModels;
using MosheSharon;
using Umbraco.Cms.Web.Website.Models;
using Umbraco.Cms.Web.Common.UmbracoContext;
using Microsoft.Extensions.Options;
using System.Net;

namespace MyProject12.Controllers
{
    public class MeshulamController : SurfaceController
    {
        private static readonly TimeSpan DuplicateReconciliationWindow = TimeSpan.FromHours(24);
        private readonly DB _db; // Entity Framework Core DB context
        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly EmailService _emailService;

        //private static readonly string MeshulamBaseAddress = "https://sandbox.meshulam.co.il/api/light/server/1.0/";
        //private readonly string CreatePaymentProcessEndpoint = "createPaymentProcess/";
        //private readonly string ApproveTransactionEndpoint = "approveTransaction/";
        //private readonly string GetTransactionInfoEndpoint = "getTransactionInfo/";
        //private readonly string GetPaymentProcessInfoEndpoint = "getPaymentProcessInfo/";
        //private readonly string RefundTransactionEndpoint = "refundTransaction/";
        //private static readonly string UpdateDirectDebitEndpoint = "updateDirectDebit/";

        //private static readonly string UserID = "530c0ed0c411ce71";
        //private readonly string YearlyPageCode = "a54f4954c06f";
        //private readonly string MonthlyPageCode = "0614c4d8b7a0";

        //private int yearlyPrice = 348; //default value in case there is an issue
        //private int monthlyPrice = 35; //default value in case there is an issue
        private dynamic account;

        private IContentTypeService contentTypeService;
        private IContentService contentService;
        private IPublishedContentQuery publishedContetnQuery;
        private MeshulamService meshulamService;
        private readonly MeshulamWebhookPayloadReader _webhookPayloadReader;
        private ILogger<MeshulamController> _logger;
        private IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IDataProtector _temporaryMemberPasswordProtector;
        private readonly PaymentsHarnessStore _paymentsHarnessStore;
        private readonly PaymentsHarnessSandboxClient _paymentsHarnessSandboxClient;
        private readonly PaymentsHarnessOptions _paymentsHarnessOptions;
        private readonly TemporaryMemberResolver _temporaryMemberResolver;

        [ActivatorUtilitiesConstructor]
        public MeshulamController(ILogger<MeshulamController> logger,EmailService emailService,IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider, IPublishedContentQuery publishedContetnQuery, IContentService contentService, IMemberManager memberManager, IMemberService memberService, DB db, MeshulamService meshulamService, MeshulamWebhookPayloadReader webhookPayloadReader, IDataProtectionProvider dataProtectionProvider, PaymentsHarnessStore paymentsHarnessStore, PaymentsHarnessSandboxClient paymentsHarnessSandboxClient, IOptions<PaymentsHarnessOptions> paymentsHarnessOptions, TemporaryMemberResolver temporaryMemberResolver)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)

        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _emailService = emailService;
            this.contentService = contentService;
            this.publishedContetnQuery = publishedContetnQuery;
            this.meshulamService = meshulamService;
            _webhookPayloadReader = webhookPayloadReader;
            _logger = logger;
            _temporaryMemberPasswordProtector = dataProtectionProvider.CreateProtector("MO6.TemporaryMembers.Password");
            _paymentsHarnessStore = paymentsHarnessStore;
            _paymentsHarnessSandboxClient = paymentsHarnessSandboxClient;
            _paymentsHarnessOptions = paymentsHarnessOptions.Value ?? new PaymentsHarnessOptions();
            _temporaryMemberResolver = temporaryMemberResolver;

            account = publishedContetnQuery.Content(3738);//publishedContetnQuery.ContentAtRoot().First().DescendantsOrSelf().FirstOrDefault(x => x.ContentType.Alias == "account");

            ////dynamic account = contentService.GetRootContent().FirstOrDefault(c => c.ContentType.Alias == "account");
            //if (account != null)
            //{
            //    yearlyPrice = ((int)account.SubscriptionYearlyMonthlyPrice) * 12;
            //    monthlyPrice = (int)account.SubscriptionDirectDebitPrice;
            //}

            //this.account = account;

            _db = db;
            _memberManager = memberManager;
            _memberService = memberService;
        }

        private bool IsHarnessEnabled()
        {
            return _paymentsHarnessOptions.Enabled;
        }

        private Task<HarnessClassification> ClassifyHarnessAsync(MeshulamWebhookPayload payload, Transaction? transaction)
        {
            if (!IsHarnessEnabled())
            {
                return Task.FromResult(new HarnessClassification { IsHarness = false, Reason = "disabled" });
            }

            var classification = _paymentsHarnessStore.Classify(payload, transaction);
            return Task.FromResult(classification);
        }

        private bool HasHarnessAccess(string? token = null)
        {
            if (!IsHarnessEnabled())
            {
                return false;
            }

            var expected = _paymentsHarnessOptions.Token?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            var candidate = token;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = Request.Query["token"];
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = Request.Headers["X-Payments-Harness-Token"];
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                var referer = Request.Headers["Referer"].ToString();
                if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(refererUri.Query);
                    candidate = query["token"];
                }
            }

            if (string.IsNullOrWhiteSpace(candidate) &&
                Request.HasFormContentType &&
                Request.Form.TryGetValue("token", out var formToken))
            {
                candidate = formToken.ToString();
            }

            return string.Equals(expected, candidate?.Trim(), StringComparison.Ordinal);
        }

        private static string H(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string EscapeBashSingleQuoted(string? value)
        {
            var input = value ?? string.Empty;
            return "'" + input.Replace("'", "'\"'\"'") + "'";
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseReplayHeaders(string? headersDump)
        {
            if (string.IsNullOrWhiteSpace(headersDump))
            {
                yield break;
            }

            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Content-Length",
                "Host"
            };

            var lines = headersDump.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var sep = line.IndexOf(':');
                if (sep <= 0 || sep >= line.Length - 1)
                {
                    continue;
                }

                var key = line[..sep].Trim();
                var value = line[(sep + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(key) ||
                    string.IsNullOrWhiteSpace(value) ||
                    ignored.Contains(key))
                {
                    continue;
                }

                yield return new KeyValuePair<string, string>(key, value);
            }
        }

        private static string BuildReplayCurl(PaymentsHarnessRawWebhook raw, string localBaseUrl = "http://localhost:5000")
        {
            var target = $"{localBaseUrl}{raw.Path}{raw.QueryString}";
            var method = string.IsNullOrWhiteSpace(raw.Method) ? "POST" : raw.Method;
            var sb = new StringBuilder();
            sb.Append("curl -i -X ").Append(method).Append(" ").Append(EscapeBashSingleQuoted(target));

            if (!string.IsNullOrWhiteSpace(raw.ContentType))
            {
                sb.Append(" -H ").Append(EscapeBashSingleQuoted($"Content-Type: {raw.ContentType}"));
            }

            foreach (var header in ParseReplayHeaders(raw.Headers))
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sb.Append(" -H ").Append(EscapeBashSingleQuoted($"{header.Key}: {header.Value}"));
            }

            if (!string.IsNullOrWhiteSpace(raw.RawBody))
            {
                sb.Append(" --data-raw ").Append(EscapeBashSingleQuoted(raw.RawBody));
            }

            return sb.ToString();
        }

        private static string BuildHeadersDump(IHeaderDictionary headers)
        {
            if (headers == null || headers.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            foreach (var h in headers)
            {
                lines.Add($"{h.Key}: {h.Value}");
            }

            return string.Join("\n", lines);
        }

        private string BuildHarnessUrl(string? message = null, bool isError = false)
        {
            var token = _paymentsHarnessOptions.Token ?? string.Empty;
            var url = $"/payments-harness?token={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrWhiteSpace(message))
            {
                url += $"&message={Uri.EscapeDataString(message)}&isError={(isError ? "1" : "0")}";
            }

            return url;
        }

        private IActionResult RedirectToHarness(string? message = null, bool isError = false)
        {
            ApplyNoCacheHeaders();
            return Redirect(BuildHarnessUrl(message, isError));
        }

        private void ApplyNoCacheHeaders()
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        private ContentResult RenderHarnessPage(string message = "", bool isError = false)
        {
            ApplyNoCacheHeaders();
            var snapshot = _paymentsHarnessStore.GetSnapshot();
            var token = _paymentsHarnessOptions.Token ?? string.Empty;
            var title = "Payments Harness";

            var html = new StringBuilder();
            html.Append("<!doctype html><html><head><meta charset='utf-8'>");
            html.Append("<title>").Append(H(title)).Append("</title>");
            html.Append("<style>body{font-family:Arial,sans-serif;padding:16px}table{border-collapse:collapse;width:100%;margin:12px 0}th,td{border:1px solid #ddd;padding:6px;font-size:12px}th{background:#f5f5f5}.ok{color:#0a7a0a}.err{color:#a40000}.box{display:flex;gap:8px;flex-wrap:wrap}.pill{background:#f3f3f3;padding:4px 8px;border-radius:999px}.raw-row{cursor:pointer}.detail-row{display:none;background:#fcfcfc}</style>");
            html.Append("</head><body>");
            html.Append("<h2>Payments Harness</h2>");
            html.Append("<div class='box'>");
            html.Append("<span class='pill'>Enabled: ").Append(IsHarnessEnabled() ? "true" : "false").Append("</span>");
            html.Append("<span class='pill'>UTC: ").Append(H(DateTime.UtcNow.ToString("u"))).Append("</span>");
            html.Append("</div>");

            if (!string.IsNullOrWhiteSpace(message))
            {
                html.Append("<p class='").Append(isError ? "err" : "ok").Append("'>").Append(H(message)).Append("</p>");
            }

            html.Append("<h3>Actions</h3>");
            html.Append("<div class='box'>");
            html.Append("<a href='/payments-harness/start-yearly?token=").Append(H(token)).Append("'>Create Yearly Test</a>");
            html.Append("<a href='/payments-harness/start-monthly?token=").Append(H(token)).Append("'>Create Monthly Test</a>");
            html.Append("<a href='/payments-harness/cancel-latest?token=").Append(H(token)).Append("'>Cancel Latest Monthly (Sandbox)</a>");
            html.Append("<a href='/payments-harness?token=").Append(H(token)).Append("'>Refresh</a>");
            html.Append("</div>");

            html.Append("<h3>Runs</h3>");
            html.Append("<table><thead><tr><th>RunId</th><th>Type</th><th>Status</th><th>Reason</th><th>Checkout</th><th>Process</th><th>DirectDebit</th><th>Updated UTC</th></tr></thead><tbody>");
            foreach (var run in snapshot.Runs)
            {
                html.Append("<tr>");
                html.Append("<td>").Append(H(run.RunId)).Append("</td>");
                html.Append("<td>").Append(H(run.Type.ToString())).Append("</td>");
                html.Append("<td>").Append(H(run.Status)).Append("</td>");
                html.Append("<td>").Append(H(run.Reason)).Append("</td>");
                html.Append("<td>");
                if (!string.IsNullOrWhiteSpace(run.CheckoutUrl))
                {
                    html.Append("<a target='_blank' href='").Append(H(run.CheckoutUrl)).Append("'>pay</a>");
                }
                html.Append("</td>");
                html.Append("<td>").Append(H(run.ProcessId?.ToString())).Append(" / ").Append(H(run.ProcessToken)).Append("</td>");
                html.Append("<td>").Append(H(run.DirectDebitId?.ToString())).Append("</td>");
                html.Append("<td>").Append(H(run.LastUpdatedUtc.ToString("u"))).Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");

            html.Append("<h3>Recurring Events</h3>");
            html.Append("<table><thead><tr><th>UTC</th><th>Path</th><th>Result</th><th>Reason</th><th>RunId</th><th>Status/Code</th><th>Amount</th><th>Asmachta</th><th>DirectDebit</th><th>ProcessToken</th><th>TxToken</th><th>Email</th></tr></thead><tbody>");
            foreach (var evt in snapshot.RecurringEvents)
            {
                html.Append("<tr>");
                html.Append("<td>").Append(H(evt.ReceivedUtc.ToString("u"))).Append("</td>");
                html.Append("<td>").Append(H(evt.Path)).Append("</td>");
                html.Append("<td>").Append(H(evt.Result)).Append("</td>");
                html.Append("<td>").Append(H(evt.Reason)).Append("</td>");
                html.Append("<td>").Append(H(evt.LinkedRunId)).Append("</td>");
                html.Append("<td>").Append(H(evt.Status)).Append(" / ").Append(H(evt.StatusCode?.ToString())).Append("</td>");
                html.Append("<td>").Append(H(evt.Amount?.ToString(CultureInfo.InvariantCulture))).Append("</td>");
                html.Append("<td>").Append(H(evt.Asmachta)).Append("</td>");
                html.Append("<td>").Append(H(evt.DirectDebitId?.ToString())).Append("</td>");
                html.Append("<td>").Append(H(evt.ProcessToken)).Append("</td>");
                html.Append("<td>").Append(H(evt.TransactionToken)).Append("</td>");
                html.Append("<td>").Append(H(evt.PayerEmail)).Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");

            html.Append("<h3>Raw Webhook Capture (All)</h3>");
            html.Append("<p>Click a row to open full request details and a replay command.</p>");
            html.Append("<table><thead><tr><th>UTC</th><th>Method</th><th>Path</th><th>Format</th><th>IsHarness</th><th>Classify</th><th>RunId</th><th>Status/Code</th><th>Tx</th></tr></thead><tbody>");
            var rawIndex = 0;
            foreach (var raw in snapshot.RawWebhooks)
            {
                var detailsId = $"raw-details-{rawIndex++}";
                html.Append("<tr class='raw-row' onclick=\"toggleRaw('").Append(H(detailsId)).Append("')\">");
                html.Append("<td>").Append(H(raw.ReceivedUtc.ToString("u"))).Append("</td>");
                html.Append("<td>").Append(H(raw.Method)).Append("</td>");
                html.Append("<td>").Append(H(raw.Path)).Append("</td>");
                html.Append("<td>").Append(H(raw.Format)).Append("</td>");
                html.Append("<td>").Append(raw.IsHarness ? "true" : "false").Append("</td>");
                html.Append("<td>").Append(H(raw.ClassificationReason)).Append("</td>");
                html.Append("<td>").Append(H(raw.LinkedRunId)).Append("</td>");
                html.Append("<td>").Append(H(raw.Status)).Append(" / ").Append(H(raw.StatusCode?.ToString())).Append("</td>");
                html.Append("<td>").Append(H(raw.TransactionId?.ToString())).Append(" / ").Append(H(raw.TransactionToken)).Append("</td>");
                html.Append("</tr>");
                html.Append("<tr id='").Append(H(detailsId)).Append("' class='detail-row'><td colspan='9'>");
                html.Append("<div><b>Query:</b> ").Append(H(raw.QueryString)).Append("</div>");
                html.Append("<div><b>Content-Type:</b> ").Append(H(raw.ContentType)).Append("</div>");
                html.Append("<div><b>CField1:</b> ").Append(H(raw.CField1)).Append("</div>");
                html.Append("<div><b>Process:</b> ").Append(H(raw.ProcessId?.ToString())).Append(" / ").Append(H(raw.ProcessToken)).Append("</div>");
                html.Append("<div><b>DirectDebit:</b> ").Append(H(raw.DirectDebitId?.ToString())).Append("</div>");
                html.Append("<div><b>Headers:</b><pre style='white-space:pre-wrap'>").Append(H(raw.Headers)).Append("</pre></div>");
                html.Append("<div><b>Raw Body:</b><pre style='white-space:pre-wrap'>").Append(H(raw.RawBody)).Append("</pre></div>");
                html.Append("<div><b>Replay Curl (local):</b><pre style='white-space:pre-wrap'>").Append(H(BuildReplayCurl(raw))).Append("</pre></div>");
                html.Append("</td></tr>");
            }
            html.Append("</tbody></table>");

            html.Append("<script>function toggleRaw(id){var el=document.getElementById(id);if(!el)return;el.style.display=(el.style.display==='table-row'?'none':'table-row');}</script>");
            html.Append("</body></html>");
            return Content(html.ToString(), "text/html; charset=utf-8");
        }

        [Route("payments-harness")]
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public IActionResult PaymentsHarness()
        {
            ApplyNoCacheHeaders();
            if (!HasHarnessAccess())
            {
                return Unauthorized("Harness disabled or invalid token");
            }

            var message = Request.Query["message"].ToString();
            var isError = string.Equals(Request.Query["isError"].ToString(), "1", StringComparison.Ordinal);
            return RenderHarnessPage(message, isError);
        }

        [Route("payments-harness/start-yearly")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> StartYearlyHarness([FromForm] string token = "")
        {
            ApplyNoCacheHeaders();
            if (!HasHarnessAccess(token))
            {
                return Unauthorized("Harness disabled or invalid token");
            }

            var run = _paymentsHarnessStore.CreateRun(HarnessRunType.Yearly);
            var cField = PaymentsHarnessStore.BuildHarnessCField(run.RunId, run.Type);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _paymentsHarnessSandboxClient.CreatePaymentProcessAsync(
                false,
                meshulamService.yearlyPrice,
                baseUrl,
                cField,
                _paymentsHarnessOptions.Token ?? string.Empty);

            if (!result.Success)
            {
                _paymentsHarnessStore.MarkRunFailed(run, result.Error);
                return RedirectToHarness($"Yearly run failed: {result.Error}", true);
            }

            _paymentsHarnessStore.MarkRunCreated(run, result.CheckoutUrl, cField);
            return RedirectToHarness($"Yearly run created: {run.RunId}");
        }

        [Route("payments-harness/start-monthly")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> StartMonthlyHarness([FromForm] string token = "")
        {
            ApplyNoCacheHeaders();
            if (!HasHarnessAccess(token))
            {
                return Unauthorized("Harness disabled or invalid token");
            }

            var run = _paymentsHarnessStore.CreateRun(HarnessRunType.Monthly);
            var cField = PaymentsHarnessStore.BuildHarnessCField(run.RunId, run.Type);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _paymentsHarnessSandboxClient.CreatePaymentProcessAsync(
                true,
                meshulamService.monthlyPrice,
                baseUrl,
                cField,
                _paymentsHarnessOptions.Token ?? string.Empty);

            if (!result.Success)
            {
                _paymentsHarnessStore.MarkRunFailed(run, result.Error);
                return RedirectToHarness($"Monthly run failed: {result.Error}", true);
            }

            _paymentsHarnessStore.MarkRunCreated(run, result.CheckoutUrl, cField);
            return RedirectToHarness($"Monthly run created: {run.RunId}");
        }

        [Route("payments-harness/cancel-latest")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CancelLatestHarness([FromForm] string token = "")
        {
            ApplyNoCacheHeaders();
            if (!HasHarnessAccess(token))
            {
                return Unauthorized("Harness disabled or invalid token");
            }

            var run = _paymentsHarnessStore.FindLatestRecurringRun();
            if (run == null || !run.TransactionId.HasValue || string.IsNullOrWhiteSpace(run.TransactionToken))
            {
                return RedirectToHarness("Cancel test failed: no monthly run with transaction identifiers", true);
            }

            var cancelResult = await _paymentsHarnessSandboxClient.UpdateDirectDebitAsync(
                run.TransactionId.Value,
                run.TransactionToken,
                run.Asmachta,
                run.DirectDebitId,
                "harness-cancel@example.test");

            var cancelRun = _paymentsHarnessStore.CreateRun(HarnessRunType.Cancel);
            if (!cancelResult.Success)
            {
                _paymentsHarnessStore.MarkRunFailed(cancelRun, cancelResult.Error);
                return RedirectToHarness($"Cancel test failed: {cancelResult.Error}", true);
            }

            _paymentsHarnessStore.MarkRunCompleted(cancelRun, "updateDirectDebit sandbox success");
            _paymentsHarnessStore.RecordHarnessEvent(
                "/payments-harness/cancel-latest",
                null,
                new Transaction
                {
                    TransactionId = run.TransactionId,
                    TransactionToken = run.TransactionToken,
                    DirectDebitId = run.DirectDebitId,
                    ProcessToken = run.ProcessToken,
                    Asmachta = run.Asmachta,
                    Status = "cancel-sent",
                    StatusCode = 2
                },
                new HarnessClassification { IsHarness = true, RunId = cancelRun.RunId, Reason = "manual-cancel" },
                "ok",
                "updateDirectDebit sandbox success");

            return RedirectToHarness("Cancel test executed");
        }

        private string ProtectTemporaryPassword(string plainPassword)
        {
            return _temporaryMemberPasswordProtector.Protect(plainPassword);
        }

        private string GetTemporaryPasswordForRegistration(string storedPassword)
        {
            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                return storedPassword;
            }

            try
            {
                return _temporaryMemberPasswordProtector.Unprotect(storedPassword);
            }
            catch
            {
                // Backward compatibility for existing rows saved before protection.
                return storedPassword;
            }
        }


        // Add this method to handle the combined registration and payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> RegisterAndSubscribe(RegisterAndSubscribeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("registerSubscribeModel", "אירעה שגיאה");
                return CurrentUmbracoPage();
            }
            if(model.AcceptTerms == false)
            {
                ModelState.AddModelError("registerSubscribeModel", "נדרשת הסכמה לתקנון");
            }

            // Check if the email is already registered
            var existingMember = await _memberManager.FindByEmailAsync(model.Email);
            if (existingMember != null)
            {
                ModelState.AddModelError("registerSubscribeModel", "כתובת האימייל הזו כבר קיימת במערכת");
                return CurrentUmbracoPage();
            }

            // Generate a unique token for this registration
            string token = Guid.NewGuid().ToString("N");

            // Store the temporary member data
            var tempMember = new TemporaryMember
            {
                Email = model.Email,
                Name = model.Name,
                Password = ProtectTemporaryPassword(model.Password),
                Phone = model.PhoneNumber,
                Token = token,
                Created = DateTime.Now,
                Processed = false
            };

            _db.TemporaryMembers.Add(tempMember);
            await _db.SaveChangesAsync();

            // Calculate the payment amount based on subscription type
            int sum = model.Monthly ? meshulamService.monthlyPrice : meshulamService.yearlyPrice;
            string pageCode = model.Monthly ? meshulamService.MonthlyPageCode : meshulamService.YearlyPageCode;

            // Prepare the URLs for success and failure
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string successUrl = baseUrl + Url.SurfaceAction("Success", "Meshulam");
            string failureUrl = baseUrl + Url.SurfaceAction("Failure", "Meshulam");
            string notifyUrl = $"{baseUrl}/meshulam-response";

            // Call the payment service, passing our token in cField1
            var result = await meshulamService.CreatePaymentProcess(
                sum.ToString(),
                "1",
                successUrl,
                failureUrl,
                model.Email,
                model.Name,
                model.PhoneNumber,
                pageCode,
                token,
                notifyUrl);  // Pass the token as cField1

            var mappedRes = meshulamService.MapCPP(result);

            if (mappedRes.Item1 == 1)
            {
                return Redirect(mappedRes.Item2);
            }
            else
            {
                ModelState.AddModelError("registerSubscribeModel", "אירעה שגיאה במעבר לעמוד התשלום");
                return CurrentUmbracoPage();
            }
        }


        [HttpGet]
        public async Task<IActionResult> Success()
        {
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member != null)
            {
                await _emailService.SendNewMemberEmail(member.Name, member.Email);
            }

            TempData["SuccessMessagePayment"] = "התשלום בוצע בהצלחה, המנוי יופעל בדקות הקרובות.";
            string url = HttpUtility.UrlEncode("//חשבון");
            return Redirect(url);
        }

        [HttpGet]
        public IActionResult Failure()
        {
            TempData["FailureMessagePayment"] = "אירעה שגיאה בביצוע התשלום.";
            string url = HttpUtility.UrlEncode("//חשבון");
            return Redirect(url);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> Subscribe(Subscription subscription)
        {
            TempData["OpenModal"] = "subscription";


                if (!account.MembershipAllowed)
                {
                    return CurrentUmbracoPage();
                }


            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Get the currently logged-in member
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                ModelState.AddModelError("Subscribe", "משתמש לא מחובר.");
                return CurrentUmbracoPage();
            }

            int sum = subscription.Monthly ? meshulamService.monthlyPrice : meshulamService.yearlyPrice;
            string pageCode = subscription.Monthly ? meshulamService.MonthlyPageCode : meshulamService.YearlyPageCode;

            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string successUrl = baseUrl + Url.SurfaceAction("Success", "Meshulam");
            string failureUrl = baseUrl + Url.SurfaceAction("Failure", "Meshulam");
            string notifyUrl = $"{baseUrl}/meshulam-response";

            var result = await meshulamService.CreatePaymentProcess(sum.ToString(), "1", successUrl, failureUrl, member.Email, member.Name, subscription.PhoneNumber, pageCode, member.Id, notifyUrl);
            var mappedRes = meshulamService.MapCPP(result);

            if (mappedRes.Item1 == 1)
            {
                return Redirect(mappedRes.Item2);

            }
            else
            {
                ModelState.AddModelError("Subscribe", "אירעה שגיאה במעבר לעמוד התשלום.");
            }

            return CurrentUmbracoPage();
        }

        private string ExtractTemporaryMemberToken(MeshulamWebhookPayload payload)
        {
            return payload.GetValue(
                "data[customFields][cField1]",
                "data[cField1]",
                "customFields[cField1]",
                "customFields.cField1",
                "data.customFields.cField1",
                "cField1");
        }

        private string ExtractPaymentReference(MeshulamWebhookPayload payload)
        {
            return ExtractTemporaryMemberToken(payload);
        }

        private async Task<MemberIdentityUser?> ResolveMemberFromPaymentReferenceAsync(string? paymentReference)
        {
            if (string.IsNullOrWhiteSpace(paymentReference) ||
                !int.TryParse(paymentReference.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var memberId))
            {
                return null;
            }

            var member = _memberService.GetById(memberId);
            if (member == null || string.IsNullOrWhiteSpace(member.Email))
            {
                return null;
            }

            return await _memberManager.FindByEmailAsync(member.Email);
        }

        private static List<string> GetTransactionReferenceCandidates(Transaction transaction)
        {
            var refs = new List<string>();
            if (transaction == null)
            {
                return refs;
            }

            if (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0)
            {
                refs.Add(transaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(transaction.TransactionToken))
            {
                refs.Add(transaction.TransactionToken.Trim());
            }

            return refs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetPrimaryTransactionReference(Transaction transaction)
        {
            return GetTransactionReferenceCandidates(transaction).FirstOrDefault() ?? string.Empty;
        }

        private static bool CanReconcileDuplicateTransaction(Transaction? persistedTransaction)
        {
            if (persistedTransaction == null)
            {
                return false;
            }

            return persistedTransaction.Created >= DateTime.Now.Subtract(DuplicateReconciliationWindow);
        }

        private static bool MembershipContainsTransaction(Membership membership, Transaction transaction)
        {
            if (membership == null || transaction == null)
            {
                return false;
            }

            var existingRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
            if (existingRefs.Count == 0)
            {
                return false;
            }

            var normalizedExisting = existingRefs
                .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetTransactionReferenceCandidates(transaction)
                .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                .Any(x => normalizedExisting.Contains(x));
        }

        private async Task<(bool paymentLinked, bool membershipChanged)> EnsureMembershipForTransactionAsync(
            MemberIdentityUser existingMember,
            Transaction transaction,
            bool monthly,
            bool persist)
        {
            var memberships = _db.Memberships
                .Where(x => x.memberID == existingMember.Id)
                .ToList();
            var transactionRefs = GetTransactionReferenceCandidates(transaction);
            var alreadyApplied = memberships.Any(x => MembershipContainsTransaction(x, transaction));
            var now = DateTime.Now;
            var membership = memberships
                .OrderByDescending(x => x.expiration >= now)
                .ThenByDescending(x => x.expiration)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            if (membership != null)
            {
                if (alreadyApplied)
                {
                    return (true, false);
                }

                var nowPlus = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1);
                var expPlus = membership.expiration.AddMonths(monthly ? 1 : 12).AddHours(1);

                membership.expiration = expPlus > nowPlus ? expPlus : nowPlus;
                membership.transactions = (membership.transactions ?? "") + string.Join(";", transactionRefs) + ";";
                membership.phone = transaction.PayerPhone;
                membership.isMonthly = monthly;
                membership.isMonthlyActive = monthly;

                if (persist)
                {
                    _db.Memberships.Update(membership);
                    await _db.SaveChangesAsync();
                }

                return (true, true);
            }

            membership = new Membership
            {
                expiration = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1),
                isMonthly = monthly,
                isMonthlyActive = monthly,
                memberID = existingMember.Id,
                phone = transaction.PayerPhone,
                transactions = string.Join(";", transactionRefs) + ";"
            };

            if (persist)
            {
                _db.Memberships.Add(membership);
                await _db.SaveChangesAsync();
            }

            return (true, true);
        }

        private static int? ParseNullableInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        private static float? ParseNullableFloat(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (float.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInvariant))
            {
                return parsedInvariant;
            }

            if (float.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("he-IL"), out var parsedHe))
            {
                return parsedHe;
            }

            return null;
        }

        private static int? ParsePaymentTypeFromText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized.Contains("הוראת") || normalized.Contains("direct"))
            {
                return 1;
            }

            if (normalized.Contains("credit") || normalized.Contains("card") || normalized.Contains("רגיל") || normalized.Contains("אשראי"))
            {
                return 2;
            }

            return null;
        }

        private static bool HasWebhookSignal(MeshulamWebhookPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(payload.GetValue(
                "data[transactionToken]",
                "transactionToken",
                "data[transactionId]",
                "transactionId",
                "data[asmachta]",
                "asmachta",
                "transactionCode",
                "data[sum]",
                "sum",
                "paymentSum",
                "data[payerEmail]",
                "payerEmail",
                "email",
                "data[directDebitId]",
                "directDebitId",
                "directDebit",
                "DirectDebit",
                "data[processToken]",
                "processToken",
                "Process",
                "webhookKey"));
        }

        private static bool IsEmptyWebhookProbe(MeshulamWebhookPayload payload)
        {
            if (payload == null)
            {
                return true;
            }

            var hasFormValues = payload.HasForm && payload.FormData != null && payload.FormData.Count > 0;
            var hasJsonValues = payload.IsJson && payload.JsonData != null && payload.JsonData.HasValues;
            if (hasFormValues || hasJsonValues)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(payload.RawBody))
            {
                return false;
            }

            return !HasWebhookSignal(payload);
        }

        private static bool IsSyntheticWebhookToken(string? token)
        {
            return !string.IsNullOrWhiteSpace(token) &&
                   token.StartsWith("wh:", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnrichTransactionFromPayload(Transaction transaction, MeshulamWebhookPayload payload)
        {
            if (transaction == null || payload == null)
            {
                return;
            }

            if (!transaction.TransactionId.HasValue || transaction.TransactionId.Value <= 0)
            {
                transaction.TransactionId = ParseNullableInt(payload.GetValue("data[transactionId]", "transactionId", "transactionCode"));
            }

            var payloadToken = payload.GetValue("data[transactionToken]", "transactionToken");
            if (!string.IsNullOrWhiteSpace(payloadToken) &&
                (string.IsNullOrWhiteSpace(transaction.TransactionToken) || IsSyntheticWebhookToken(transaction.TransactionToken)))
            {
                transaction.TransactionToken = payloadToken;
            }

            if (!transaction.DirectDebitId.HasValue || transaction.DirectDebitId.Value <= 0)
            {
                transaction.DirectDebitId = ParseNullableInt(payload.GetValue(
                    "data[directDebitId]",
                    "directDebitId",
                    "directDebit",
                    "DirectDebit"));
            }

            if (!transaction.ProcessId.HasValue || transaction.ProcessId.Value <= 0)
            {
                transaction.ProcessId = ParseNullableInt(payload.GetValue("data[processId]", "processId", "ProcessId"));
            }

            if (string.IsNullOrWhiteSpace(transaction.ProcessToken))
            {
                transaction.ProcessToken = payload.GetValue("data[processToken]", "processToken", "ProcessToken", "Process", "webhookKey") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.PayerEmail))
            {
                transaction.PayerEmail = payload.GetValue("data[payerEmail]", "payerEmail", "email", "customerEmail") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.PayerPhone))
            {
                transaction.PayerPhone = payload.GetValue("data[payerPhone]", "payerPhone", "phone") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.FullName))
            {
                transaction.FullName = payload.GetValue("data[fullName]", "fullName", "customerName", "payer_name") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.Description))
            {
                transaction.Description = payload.GetValue("data[description]", "description", "paymentDesc") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.Asmachta))
            {
                transaction.Asmachta = payload.GetValue("data[asmachta]", "asmachta", "transactionCode") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(transaction.PaymentDate))
            {
                transaction.PaymentDate = payload.GetValue("data[paymentDate]", "paymentDate") ?? string.Empty;
            }

            if (!transaction.Sum.HasValue)
            {
                transaction.Sum = ParseNullableFloat(payload.GetValue("data[sum]", "sum", "paymentSum"));
            }

            if (!transaction.PaymentType.HasValue)
            {
                var paymentTypeRaw = payload.GetValue("data[paymentType]", "paymentType");
                transaction.PaymentType = ParseNullableInt(paymentTypeRaw) ?? ParsePaymentTypeFromText(paymentTypeRaw);
            }

            if (!transaction.StatusCode.HasValue)
            {
                transaction.StatusCode = ParseNullableInt(payload.GetValue("data[statusCode]", "statusCode"));
            }

            if (string.IsNullOrWhiteSpace(transaction.Status))
            {
                transaction.Status = payload.GetValue("data[status]", "status") ?? string.Empty;
            }

            if (!transaction.StatusCode.HasValue && (!string.IsNullOrWhiteSpace(transaction.Asmachta) || transaction.DirectDebitId.HasValue))
            {
                transaction.StatusCode = 2;
            }

            if (string.IsNullOrWhiteSpace(transaction.Status) && transaction.StatusCode == 2)
            {
                transaction.Status = "שולם";
            }
        }

        private Transaction MapSuccessWebhookTransaction(MeshulamWebhookPayload payload)
        {
            if (payload == null)
            {
                return null;
            }

            Transaction mapped = null;
            if (payload.IsJson)
            {
                mapped = meshulamService.MapJsonToTransaction(payload.RawBody);
            }
            else if (payload.HasForm)
            {
                mapped = meshulamService.MapFormDataToTransaction(payload.FormData);
            }
            else
            {
                mapped = MeshulamTransactionMapper.MapLoosePayloadToTransaction(payload);
            }

            if (mapped != null)
            {
                EnrichTransactionFromPayload(mapped, payload);
            }

            return mapped;
        }

        private Transaction MapFailureWebhookTransaction(MeshulamWebhookPayload payload)
        {
            var transaction = MapSuccessWebhookTransaction(payload) ?? new Transaction
            {
                Created = DateTime.Now,
                Status = string.Empty,
                TransactionToken = string.Empty,
                PaymentDate = string.Empty,
                Asmachta = string.Empty,
                Description = string.Empty,
                FullName = string.Empty,
                PayerPhone = string.Empty,
                PayerEmail = string.Empty,
                CardSuffix = string.Empty,
                CardType = string.Empty,
                CardBrand = string.Empty,
                CardExp = string.Empty,
                ProcessToken = string.Empty,
                CardToken = string.Empty
            };
            transaction.Created = DateTime.Now;

            transaction.Status = payload.GetValue("data[status]", "status", "error_status", "errorStatus") ?? "חיוב נכשל";
            transaction.StatusCode = 0;

            transaction.PayerEmail = payload.GetValue("data[payerEmail]", "payerEmail", "email") ?? transaction.PayerEmail;
            transaction.PayerPhone = payload.GetValue("data[payerPhone]", "payerPhone", "phone") ?? transaction.PayerPhone;
            transaction.FullName = payload.GetValue("data[fullName]", "fullName", "payer_name", "customerName") ?? transaction.FullName;
            transaction.Description = payload.GetValue("description", "data[description]", "paymentDesc") ?? transaction.Description;

            var errorMessage = payload.GetValue("error_message", "err", "error", "data[err]");
            transaction.ProcessToken = !string.IsNullOrWhiteSpace(errorMessage)
                ? $"סיבה: {errorMessage}"
                : (transaction.ProcessToken ?? "חיוב נכשל");

            if (string.IsNullOrWhiteSpace(transaction.TransactionToken))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.RawBody ?? Guid.NewGuid().ToString("N")));
                transaction.TransactionToken = "fail:" + Convert.ToHexString(hash).ToLowerInvariant();
            }

            return transaction;
        }

        private async Task<IActionResult> HandleMeshulam(MeshulamWebhookPayload payload)
        {
            try
            {
                string paymentReference = ExtractPaymentReference(payload);
                var transaction = MapSuccessWebhookTransaction(payload);
                var classification = await ClassifyHarnessAsync(payload, transaction);
                _paymentsHarnessStore.RecordRawWebhook(
                    "/meshulam-response",
                    payload,
                    transaction,
                    classification,
                    Request.Method,
                    Request.QueryString.Value,
                    BuildHeadersDump(Request.Headers));
                var isHarness = classification.IsHarness;
                var persist = !isHarness;

                if (transaction == null || string.IsNullOrWhiteSpace(transaction.TransactionToken))
                {
                    if (IsEmptyWebhookProbe(payload))
                    {
                        _logger.LogInformation("Ignoring empty Meshulam callback probe. method={Method}, path=/meshulam-response", Request.Method);
                        if (isHarness)
                        {
                            _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "ok", "empty-probe-ignored");
                        }

                        return Ok();
                    }

                    if (isHarness)
                    {
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "failed", "invalid-transaction-payload");
                        return Ok();
                    }
                    return BadRequest("Invalid transaction payload");
                }

                if (!meshulamService.IsSuccessfulTransaction(transaction))
                {
                    if (isHarness)
                    {
                        _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "failed", "non-success-status");
                    }
                    _logger.LogWarning("Ignoring non-success transaction callback. token={TransactionToken}, status={Status}, statusCode={StatusCode}",
                        transaction.TransactionToken, transaction.Status, transaction.StatusCode);
                    return Ok();
                }

                if (!meshulamService.IsExpectedMembershipAmount(transaction))
                {
                    if (isHarness)
                    {
                        _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "failed", "unexpected-amount-ignored");
                    }

                    _logger.LogWarning("Ignoring successful payment callback with unexpected amount. token={TransactionToken}, sum={Sum}, expectedMonthly={MonthlyPrice}, expectedYearly={YearlyPrice}, description={Description}",
                        transaction.TransactionToken,
                        transaction.Sum,
                        meshulamService.monthlyPrice,
                        meshulamService.yearlyPrice,
                        transaction.Description);
                    return Ok();
                }

                var persistedDuplicateTransaction = isHarness
                    ? null
                    : _db.Transactions.FirstOrDefault(x => x.TransactionToken == transaction.TransactionToken);
                var duplicate = isHarness
                    ? _paymentsHarnessStore.HasSeenTransactionToken(transaction.TransactionToken)
                    : persistedDuplicateTransaction != null;
                if (duplicate && isHarness)
                {
                    _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                    _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "ok", "duplicate-ignored");
                    _logger.LogInformation("Duplicate callback ignored. token={TransactionToken}", transaction.TransactionToken);
                    return Ok();
                }

                if (duplicate)
                {
                    if (!CanReconcileDuplicateTransaction(persistedDuplicateTransaction))
                    {
                        _logger.LogInformation("Duplicate callback ignored outside reconciliation window. token={TransactionToken}, created={Created}",
                            transaction.TransactionToken,
                            persistedDuplicateTransaction?.Created);
                        return Ok();
                    }

                    _logger.LogInformation("Duplicate callback received, resuming reconciliation. token={TransactionToken}", transaction.TransactionToken);
                }

                // Approve transaction
                bool hasProviderIdentifiersForApproval = transaction.TransactionId.HasValue &&
                                                         transaction.TransactionId.Value > 0 &&
                                                         !string.IsNullOrWhiteSpace(transaction.TransactionToken) &&
                                                         !transaction.TransactionToken.StartsWith("wh:", StringComparison.Ordinal);
                if (!duplicate && hasProviderIdentifiersForApproval)
                {
                    bool approveSucceeded = false;
                    for (int attempt = 0; attempt < 2 && !approveSucceeded; attempt++)
                    {
                        approveSucceeded = await meshulamService.ApproveTransaction(transaction);
                    }

                    if (!approveSucceeded)
                    {
                        // Per provider guidance: a paid callback can still be processed if approve fails.
                        _logger.LogWarning("ApproveTransaction failed, continuing callback processing. token={TransactionToken}, transactionId={TransactionId}",
                            transaction.TransactionToken,
                            transaction.TransactionId);

                        if (isHarness)
                        {
                            _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                            _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "ok", "approval-failed-continued");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Skipping ApproveTransaction due to missing provider identifiers. token={TransactionToken}, transactionId={TransactionId}",
                        transaction.TransactionToken,
                        transaction.TransactionId);
                }

                if (persist)
                {
                    if (!duplicate)
                    {
                        _db.Transactions.Add(transaction);
                        await _db.SaveChangesAsync();
                    }
                }

                bool monthly = meshulamService.IsMonthlyTransaction(transaction);

                var tempMember = !string.IsNullOrWhiteSpace(paymentReference)
                    ? _temporaryMemberResolver.ResolveByToken(paymentReference)
                    : null;
                var tempMemberWasProcessed = tempMember?.Processed ?? false;
                var createdNewMember = false;

                var existingMember = await ResolveMemberFromPaymentReferenceAsync(paymentReference);
                if (existingMember == null && !string.IsNullOrWhiteSpace(transaction.PayerEmail))
                {
                    existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);
                }

                if (existingMember == null && tempMember != null && !string.IsNullOrWhiteSpace(tempMember.Email))
                {
                    existingMember = await _memberManager.FindByEmailAsync(tempMember.Email);
                }

                if (existingMember == null && transaction.DirectDebitId.HasValue)
                {
                    var fallbackTransactions = _db.Transactions
                        .Where(x => x.DirectDebitId == transaction.DirectDebitId &&
                                    !string.IsNullOrWhiteSpace(x.PayerEmail) &&
                                    x.TransactionToken != transaction.TransactionToken &&
                                    (!transaction.TransactionId.HasValue || x.TransactionId != transaction.TransactionId))
                        .OrderByDescending(x => x.Created)
                        .ToList();

                    foreach (var previousDirectDebitTransaction in fallbackTransactions)
                    {
                        existingMember = await _memberManager.FindByEmailAsync(previousDirectDebitTransaction.PayerEmail);
                        if (existingMember != null)
                        {
                            break;
                        }
                    }
                }

                if (existingMember == null &&
                    tempMember == null &&
                    string.IsNullOrWhiteSpace(paymentReference))
                {
                    tempMember = _temporaryMemberResolver.ResolveNewestUnprocessedByExactEmail(transaction.PayerEmail);
                    tempMemberWasProcessed = tempMember?.Processed ?? false;

                    if (tempMember != null)
                    {
                        _logger.LogInformation("Resolved TemporaryMember via exact payerEmail fallback. email={Email}, tempMemberId={TemporaryMemberId}, transactionToken={TransactionToken}",
                            transaction.PayerEmail,
                            tempMember.Id,
                            transaction.TransactionToken);
                    }
                }

                if (existingMember == null && tempMember != null && !string.IsNullOrWhiteSpace(tempMember.Email))
                {
                    existingMember = await _memberManager.FindByEmailAsync(tempMember.Email);
                }
                
                if (persist && existingMember == null && tempMember != null && !tempMemberWasProcessed)
                {
                    var registerModel = new RegisterModel
                    {
                        Name = tempMember.Name,
                        Email = tempMember.Email,
                        Password = GetTemporaryPasswordForRegistration(tempMember.Password),
                        Username = tempMember.Email,
                        UsernameIsEmail = true,
                        MemberTypeAlias = Constants.Conventions.MemberTypes.DefaultAlias,
                        RedirectUrl = null,
                        AutomaticLogIn = false,
                        MemberProperties = new List<MemberPropertyModel>()
                    };

                    var identityUser = MemberIdentityUser.CreateNew(
                        registerModel.Username,
                        registerModel.Email,
                        registerModel.MemberTypeAlias,
                        false,
                        registerModel.Name);

                    var identityResult = await _memberManager.CreateAsync(identityUser, registerModel.Password);
                    if (!identityResult.Succeeded)
                    {
                        var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
                        _logger.LogWarning("Combined payment member creation failed. email={Email}, token={Token}, transactionToken={TransactionToken}, errors={Errors}",
                            tempMember.Email,
                            paymentReference,
                            transaction.TransactionToken,
                            errors);
                    }
                    else
                    {
                        createdNewMember = true;
                    }

                    existingMember = await _memberManager.FindByEmailAsync(tempMember.Email);
                }

                var paymentLinked = false;
                var membershipChanged = false;

                if (existingMember != null)
                {
                    (paymentLinked, membershipChanged) = await EnsureMembershipForTransactionAsync(existingMember, transaction, monthly, persist);

                    if (persist && tempMember != null && paymentLinked && !tempMember.Processed)
                    {
                        tempMember.Processed = true;
                        _db.TemporaryMembers.Update(tempMember);
                        await _db.SaveChangesAsync();
                    }

                    if (persist && membershipChanged)
                    {
                        try
                        {
                            foreach (string email in ((string)account.NewMembershipEmailToManagement).Split(','))
                            {
                                await _emailService.SendManagementNewMembershipEmail(
                                    existingMember.Name,
                                    existingMember.Email,
                                    monthly,
                                    email,
                                    string.IsNullOrWhiteSpace(transaction.PayerPhone) ? tempMember?.Phone : transaction.PayerPhone);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send management membership notification. email={Email}, transactionToken={TransactionToken}", existingMember.Email, transaction.TransactionToken);
                        }
                    }

                    if (persist && tempMember != null && paymentLinked && !tempMemberWasProcessed)
                    {
                        try
                        {
                            if (!existingMember.IsApproved)
                            {
                                await _emailService.GenerateSendVerificationEmail(tempMember.Name, tempMember.Email, Url);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send verification email after successful combined payment. email={Email}, transactionToken={TransactionToken}", tempMember.Email, transaction.TransactionToken);
                        }

                        try
                        {
                            foreach (string email in ((string)account.NewMemberEmailToManagement).Split(','))
                            {
                                await _emailService.SendManagementNewMemberEmail(existingMember.Name, existingMember.Email, email);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send management member notification. email={Email}, transactionToken={TransactionToken}", existingMember.Email, transaction.TransactionToken);
                        }

                        if (createdNewMember)
                        {
                            _logger.LogInformation("Completed combined payment registration. email={Email}, transactionToken={TransactionToken}", existingMember.Email, transaction.TransactionToken);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Received successful payment but no member could be resolved. email={Email}, paymentReference={PaymentReference}, transactionToken={TransactionToken}",
                        transaction.PayerEmail,
                        paymentReference,
                        transaction.TransactionToken);
                }

                if (isHarness)
                {
                    _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                    _paymentsHarnessStore.RecordHarnessEvent("/meshulam-response", payload, transaction, classification, "ok", "processed-no-db");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [Route("meshulam-response")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> HandleMeshulamResponse9jf83207409f27()
        {
            var payload = await _webhookPayloadReader.ReadAsync(Request);
            return await HandleMeshulam(payload);
            //    _logger.LogInformation("Starting HandleMeshulamResponse9jf83207409f27 - Regular Transaction Handler");
            //    try
            //    {
            //        // Get form data
            //        var formData = await Request.ReadFormAsync();
            //        _logger.LogDebug("Form data received. Keys: {FormDataKeys}", string.Join(", ", formData.Keys));

            //        // Extract token if regiter + membership
            //        string token = null;
            //        if (formData.ContainsKey("data[cField1]"))
            //        {
            //            token = formData["data[cField1]"];
            //        }

            //        // Map form data to transaction
            //        var transaction = meshulamService.MapFormDataToTransaction(formData);
            //        _logger.LogInformation("Transaction mapped. TransactionToken: {TransactionToken}, Amount: {Amount}, Email: {Email}",transaction.TransactionToken, transaction.Sum, transaction.PayerEmail);

            //        // Approve transaction
            //        try
            //        {
            //            _logger.LogDebug("Attempting to approve transaction {TransactionToken}", transaction.TransactionToken);
            //            bool success = await meshulamService.ApproveTransaction(transaction);

            //            if (!success)
            //            {
            //                _logger.LogWarning("First attempt to approve transaction {TransactionToken} failed. Retrying...", transaction.TransactionToken);
            //                success = await meshulamService.ApproveTransaction(transaction);

            //                if (!success)
            //                {
            //                    _logger.LogError("Both attempts to approve transaction {TransactionToken} failed", transaction.TransactionToken);
            //                    throw new Exception("Failed to approve transaction after two attempts");
            //                }
            //            }

            //            _logger.LogInformation("Transaction {TransactionToken} approved successfully", transaction.TransactionToken);
            //        }
            //        catch (Exception ex)
            //        {
            //            _logger.LogError(ex, "Error approving transaction {TransactionToken}", transaction.TransactionToken);
            //        }

            //        // Save transaction if not already exists
            //        if (!_db.Transactions.Any(x => x.TransactionToken == transaction.TransactionToken))
            //        {
            //            _logger.LogInformation("Transaction {TransactionToken} is new, saving to database", transaction.TransactionToken);
            //            _db.Transactions.Add(transaction);
            //            await _db.SaveChangesAsync();
            //            _logger.LogDebug("Transaction {TransactionToken} saved successfully", transaction.TransactionToken);

            //            bool monthly = ((int)transaction.Sum) == meshulamService.monthlyPrice;
            //            _logger.LogInformation("Subscription type: {IsMonthly}", monthly ? "Monthly" : "Yearly");

            //            // Process new user registration if token is present
            //            string memberId = null;

            //            if (!string.IsNullOrEmpty(token))
            //            {
            //                _logger.LogInformation("Processing registration for token {Token}", token);

            //                // Find temporary member by token
            //                var tempMember = _db.TemporaryMembers.FirstOrDefault(t => t.Token == token && !t.Processed);
            //                if (tempMember != null)
            //                {
            //                    _logger.LogInformation("Found temporary member for token {Token}. Name: {Name}, Email: {Email}",
            //                        token, tempMember.Name, tempMember.Email);

            //                    // Register the new member
            //                    var registerModel = new RegisterModel
            //                    {
            //                        Name = tempMember.Name,
            //                        Email = tempMember.Email,
            //                        Password = tempMember.Password,
            //                        Username = tempMember.Email,
            //                        UsernameIsEmail = true,
            //                        MemberTypeAlias = Constants.Conventions.MemberTypes.DefaultAlias,
            //                        RedirectUrl = null,
            //                        AutomaticLogIn = false,
            //                        MemberProperties = new List<MemberPropertyModel>()
            //                    };

            //                    _logger.LogDebug("Creating member identity for {Email}", tempMember.Email);
            //                    var identityUser = MemberIdentityUser.CreateNew(
            //                        registerModel.Username,
            //                        registerModel.Email,
            //                        registerModel.MemberTypeAlias,
            //                        false, // isApproved - will be set to true after verification
            //                        registerModel.Name);

            //                    _logger.LogDebug("Attempting to create member in database for {Email}", tempMember.Email);
            //                    IdentityResult identityResult = await _memberManager.CreateAsync(
            //                        identityUser,
            //                        registerModel.Password);

            //                    if (identityResult.Succeeded)
            //                    {
            //                        _logger.LogInformation("Successfully created new member with email {Email}", tempMember.Email);
            //                        memberId = identityUser.Id;

            //                        // Mark temp member as processed
            //                        tempMember.Processed = true;
            //                        _db.TemporaryMembers.Update(tempMember);
            //                        await _db.SaveChangesAsync();
            //                        _logger.LogDebug("Marked temporary member {Email} as processed", tempMember.Email);

            //                        // Send verification email
            //                        _logger.LogDebug("Creating email verification for {Email}", tempMember.Email);
            //                        EmailVerification ev = new EmailVerification
            //                        {
            //                            Code = Helpers.GenerateUniqueCode(5),
            //                            Created = DateTime.Now,
            //                            Email = tempMember.Email,
            //                            TimesSent = 0
            //                        };
            //                        _db.EmailVerifications.Add(ev);
            //                        await _db.SaveChangesAsync();

            //                        var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });
            //                        _logger.LogDebug("Sending verification email to {Email} with code {Code}", tempMember.Email, ev.Code);
            //                        await _emailService.SendVerificationNewMemberEmail(tempMember.Name, ev.Email, ev.Code, link);
            //                        _logger.LogInformation("Verification email sent to {Email}", tempMember.Email);
            //                    }
            //                    else
            //                    {
            //                        var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
            //                        _logger.LogError("Failed to create member {Email}. Errors: {Errors}", tempMember.Email, errors);
            //                    }
            //                }
            //                else
            //                {
            //                    _logger.LogWarning("No temporary member found for token {Token} or already processed", token);
            //                }
            //            }

            //            // If no new registration or registration failed, find existing member
            //            if (memberId == null)
            //            {
            //                _logger.LogInformation("Looking for existing member with email {Email}", transaction.PayerEmail);
            //                var existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);
            //                if (existingMember != null)
            //                {
            //                    _logger.LogInformation("Found existing member with email {Email}", transaction.PayerEmail);
            //                    memberId = existingMember.Id;
            //                }
            //                else
            //                {
            //                    _logger.LogWarning("No existing member found with email {Email}", transaction.PayerEmail);
            //                }
            //            }

            //            if (memberId == null)
            //            {
            //                _logger.LogError("Cannot find or create member for email {Email}", transaction.PayerEmail);
            //                return StatusCode(500, "אירעה שגיאה - לא ניתן למצוא את החבר");
            //            }

            //            // Create or update membership
            //            _logger.LogInformation("Processing membership for member ID {MemberId}", memberId);
            //            Membership membership = _db.Memberships.FirstOrDefault(x => x.memberID == memberId);

            //            if (membership != null)
            //            {
            //                _logger.LogInformation("Updating existing membership for member {MemberId}. Current expiration: {CurrentExpiration}",
            //                    memberId, membership.expiration);

            //                var nowPlus = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1);
            //                var expPlus = membership.expiration.AddMonths(monthly ? 1 : 12).AddHours(1);

            //                membership.expiration = expPlus > nowPlus ? expPlus : nowPlus;
            //                membership.transactions += transaction.TransactionId + ";";
            //                membership.phone = transaction.PayerPhone;
            //                membership.isMonthly = monthly;
            //                membership.isMonthlyActive = monthly;

            //                _logger.LogDebug("Updated membership. New expiration: {NewExpiration}, IsMonthly: {IsMonthly}, IsMonthlyActive: {IsMonthlyActive}",
            //                    membership.expiration, membership.isMonthly, membership.isMonthlyActive);

            //                _db.Memberships.Update(membership);
            //            }
            //            else
            //            {
            //                _logger.LogInformation("Creating new membership for member {MemberId}", memberId);

            //                membership = new Membership
            //                {
            //                    expiration = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1),
            //                    isMonthly = monthly,
            //                    isMonthlyActive = monthly,
            //                    memberID = memberId,
            //                    phone = transaction.PayerPhone,
            //                    transactions = transaction.TransactionId + ";"
            //                };

            //                _logger.LogDebug("New membership created. Expiration: {Expiration}, IsMonthly: {IsMonthly}, IsMonthlyActive: {IsMonthlyActive}",
            //                    membership.expiration, membership.isMonthly, membership.isMonthlyActive);

            //                _db.Memberships.Add(membership);
            //            }

            //            await _db.SaveChangesAsync();
            //            _logger.LogInformation("Membership saved successfully for member {MemberId}", memberId);
            //        }
            //        else
            //        {
            //            _logger.LogInformation("Transaction {TransactionToken} already exists in database, skipping processing", transaction.TransactionToken);
            //        }

            //        _logger.LogInformation("Successfully completed HandleMeshulamResponse9jf83207409f27");
            //        return Ok();
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogError(ex, "Error in HandleMeshulamResponse9jf83207409f27: {ErrorMessage}", ex.Message);

            //        // Log inner exceptions if present
            //        Exception innerEx = ex.InnerException;
            //        int depth = 1;
            //        while (innerEx != null)
            //        {
            //            _logger.LogError("Inner exception level {Depth}: {ErrorType} - {ErrorMessage}",
            //                depth, innerEx.GetType().Name, innerEx.Message);
            //            innerEx = innerEx.InnerException;
            //            depth++;
            //        }

            //        // Log stack trace for debugging
            //        _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);

            //        return StatusCode(500, ex.Message);
            //    }
        }

        //meshulam-dd-failure
        [Route("meshulam-dd-failure")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> HandleMeshulamResponse2847g93j565745()
        {
            try
            {
                var payload = await _webhookPayloadReader.ReadAsync(Request);
                if (IsEmptyWebhookProbe(payload))
                {
                    var emptyClassification = await ClassifyHarnessAsync(payload, null);
                    _paymentsHarnessStore.RecordRawWebhook(
                        "/meshulam-dd-failure",
                        payload,
                        null,
                        emptyClassification,
                        Request.Method,
                        Request.QueryString.Value,
                        BuildHeadersDump(Request.Headers));
                    _logger.LogInformation("Ignoring empty Meshulam recurring-failure callback probe. method={Method}", Request.Method);
                    if (emptyClassification.IsHarness)
                    {
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-failure", payload, null, emptyClassification, "ok", "empty-probe-ignored");
                    }

                    return Ok();
                }

                string paymentReference = ExtractPaymentReference(payload);
                var transaction = MapFailureWebhookTransaction(payload);
                var classification = await ClassifyHarnessAsync(payload, transaction);
                _paymentsHarnessStore.RecordRawWebhook(
                    "/meshulam-dd-failure",
                    payload,
                    transaction,
                    classification,
                    Request.Method,
                    Request.QueryString.Value,
                    BuildHeadersDump(Request.Headers));
                var isHarness = classification.IsHarness;
                var persist = !isHarness;

                if (transaction != null &&
                    !string.IsNullOrWhiteSpace(transaction.TransactionToken) &&
                    !(isHarness
                        ? _paymentsHarnessStore.HasSeenTransactionToken(transaction.TransactionToken)
                        : _db.Transactions.Any(x => x.TransactionToken == transaction.TransactionToken)))
                {
                    if (persist)
                    {
                        _db.Transactions.Add(transaction);
                        await _db.SaveChangesAsync();
                    }
                }

                if (!string.IsNullOrWhiteSpace(paymentReference))
                {
                    _logger.LogInformation("Keeping temporary member record after failure callback for later reconciliation. paymentReference={PaymentReference}, transactionToken={TransactionToken}",
                        paymentReference,
                        transaction?.TransactionToken);
                }

                if (isHarness)
                {
                    _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                    _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-failure", payload, transaction, classification, "failed", "recurring-failure-callback");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        ////meshulam-dd-success[Route("meshulam-dd-success")]
        //[Route("meshulam-dd-success")]
        //[HttpPost]
        //[IgnoreAntiforgeryToken]
        //public async Task<IActionResult> HandleMeshulamResponse2847g93j596034()
        //{
        //    var formData = await Request.ReadFormAsync();
        //    return await HandleMeshulam(formData);
        //    //    _logger.LogInformation("Starting HandleMeshulamResponse2847g93j596034 - Direct Debit Success Handler");
        //    //    try
        //    //    {
        //    //        // Get form data
        //    //        var formData = await Request.ReadFormAsync();
        //    //        _logger.LogDebug("Form data received. Keys: {FormDataKeys}", string.Join(", ", formData.Keys));

        //    //        // Extract token if regiter + membership
        //    //        string token = null;
        //    //        if (formData.ContainsKey("data[cField1]"))
        //    //        {
        //    //            token = formData["data[cField1]"];
        //    //        }

        //    //        // Map form data to transaction
        //    //        var transaction = meshulamService.MapFormDataToTransaction(formData);

        //    //        _logger.LogInformation("Adding successful transaction record for email {Email}", transaction.PayerEmail);
        //    //        _db.Transactions.Add(transaction);
        //    //        await _db.SaveChangesAsync();
        //    //        _logger.LogDebug("Successful transaction record saved");



        //    //        // Approve transaction
        //    //        try
        //    //        {
        //    //            _logger.LogDebug("Attempting to approve transaction {TransactionToken}", transaction.TransactionToken);
        //    //            bool success = await meshulamService.ApproveTransaction(transaction);

        //    //            if (!success)
        //    //            {
        //    //                _logger.LogWarning("First attempt to approve transaction {TransactionToken} failed. Retrying...", transaction.TransactionToken);
        //    //                success = await meshulamService.ApproveTransaction(transaction);

        //    //                if (!success)
        //    //                {
        //    //                    _logger.LogError("Both attempts to approve transaction {TransactionToken} failed", transaction.TransactionToken);
        //    //                    throw new Exception("Failed to approve transaction after two attempts");
        //    //                }
        //    //            }

        //    //            _logger.LogInformation("Transaction {TransactionToken} approved successfully", transaction.TransactionToken);
        //    //        }
        //    //        catch (Exception ex)
        //    //        {
        //    //            _logger.LogError(ex, "Error approving transaction {TransactionToken}", transaction.TransactionToken);
        //    //        }


        //    //        // Process new user registration if token is present
        //    //        var existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);

        //    //        if (existingMember == null && !string.IsNullOrEmpty(token))
        //    //        {
        //    //            _logger.LogInformation("Processing registration for token {Token}", token);

        //    //            // Find temporary member by token
        //    //            var tempMember = _db.TemporaryMembers.FirstOrDefault(t => t.Token == token && !t.Processed);

        //    //            if (tempMember != null)
        //    //            {
        //    //                _logger.LogInformation("Found temporary member for token {Token}. Name: {Name}, Email: {Email}",
        //    //                    token, tempMember.Name, tempMember.Email);

        //    //                // Register the new member
        //    //                var registerModel = new RegisterModel
        //    //                {
        //    //                    Name = tempMember.Name,
        //    //                    Email = tempMember.Email,
        //    //                    Password = tempMember.Password,
        //    //                    Username = tempMember.Email,
        //    //                    UsernameIsEmail = true,
        //    //                    MemberTypeAlias = Constants.Conventions.MemberTypes.DefaultAlias,
        //    //                    RedirectUrl = null,
        //    //                    AutomaticLogIn = false,
        //    //                    MemberProperties = new List<MemberPropertyModel>()
        //    //                };

        //    //                _logger.LogDebug("Creating member identity for {Email}", tempMember.Email);
        //    //                var identityUser = MemberIdentityUser.CreateNew(
        //    //                    registerModel.Username,
        //    //                    registerModel.Email,
        //    //                    registerModel.MemberTypeAlias,
        //    //                    false, // isApproved - will be set to true after verification
        //    //                    registerModel.Name);

        //    //                _logger.LogDebug("Attempting to create member in database for {Email}", tempMember.Email);
        //    //                IdentityResult identityResult = await _memberManager.CreateAsync(
        //    //                    identityUser,
        //    //                    registerModel.Password);

        //    //                if (identityResult.Succeeded)
        //    //                {
        //    //                    _logger.LogInformation("Successfully created new member with email {Email}", tempMember.Email);
        //    //                    existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);

        //    //                    // Mark temp member as processed
        //    //                    tempMember.Processed = true;
        //    //                    _db.TemporaryMembers.Update(tempMember);
        //    //                    await _db.SaveChangesAsync();
        //    //                    _logger.LogDebug("Marked temporary member {Email} as processed", tempMember.Email);

        //    //                    // Send verification email
        //    //                    _logger.LogDebug("Creating email verification for {Email}", tempMember.Email);
        //    //                    EmailVerification ev = new EmailVerification
        //    //                    {
        //    //                        Code = Helpers.GenerateUniqueCode(5),
        //    //                        Created = DateTime.Now,
        //    //                        Email = tempMember.Email,
        //    //                        TimesSent = 0
        //    //                    };
        //    //                    _db.EmailVerifications.Add(ev);
        //    //                    await _db.SaveChangesAsync();

        //    //                    //var umbracoContext = _umbracoContextAccessor.GetRequiredUmbracoContext();
        //    //                    //var homePage = umbracoContext.Content.GetAtRoot().FirstOrDefault(x => x.IsPublished());

        //    //                    var link = Url.SurfaceAction("VerificationLogin", "CustomLogin", new { email = ev.Email, code = ev.Code });
        //    //                    _logger.LogDebug("Sending verification email to {Email} with code {Code}", tempMember.Email, ev.Code);
        //    //                    await _emailService.SendVerificationNewMemberEmail(tempMember.Name, ev.Email, ev.Code, link);
        //    //                    _logger.LogInformation("Verification email sent to {Email}", tempMember.Email);
        //    //                }
        //    //                else
        //    //                {
        //    //                    var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
        //    //                    _logger.LogError("Failed to create member {Email}. Errors: {Errors}", tempMember.Email, errors);
        //    //                }
        //    //            }
        //    //            else
        //    //            {
        //    //                _logger.LogWarning("No temporary member found for token {Token} or already processed", token);
        //    //            }
        //    //        }

        //    //        //// If no new registration or registration failed, find existing member
        //    //        //if (memberId == null)
        //    //        //{
        //    //        //    _logger.LogInformation("Looking for existing member with email {Email}", transaction.PayerEmail);
        //    //        //    var existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);

        //    //        //    if (existingMember != null)
        //    //        //    {
        //    //        //        _logger.LogInformation("Found existing member with email {Email}", transaction.PayerEmail);
        //    //        //        memberId = existingMember.Id;
        //    //        //    }
        //    //        //    else
        //    //        //    {
        //    //        //        _logger.LogWarning("No existing member found with email {Email}", transaction.PayerEmail);
        //    //        //    }
        //    //        //}

        //    //        if (existingMember != null)
        //    //        {
        //    //            // Update membership for the recurring payment
        //    //            _logger.LogInformation("Processing membership for member ID {MemberId}", existingMember.Id);
        //    //            Membership membership = _db.Memberships.FirstOrDefault(x => x.memberID == existingMember.Id);

        //    //            if (membership != null)
        //    //            {
        //    //                _logger.LogInformation("Updating existing membership for member {MemberId}. Current expiration: {CurrentExpiration}",
        //    //                    existingMember.Id, membership.expiration);

        //    //                // Update expiration date
        //    //                DateTime newExpiration = membership.expiration.AddMonths(1) > DateTime.Now.AddMonths(1)
        //    //                    ? membership.expiration.AddMonths(1)
        //    //                    : DateTime.Now.AddMonths(1);

        //    //                membership.expiration = newExpiration;

        //    //                // Set subscription type
        //    //                membership.isMonthly = true;
        //    //                membership.isMonthlyActive = true;

        //    //                _logger.LogDebug("Updated membership. New expiration: {NewExpiration}, IsMonthly: {IsMonthly}, IsMonthlyActive: {IsMonthlyActive}",
        //    //                    membership.expiration, membership.isMonthly, membership.isMonthlyActive);

        //    //                // Update the membership
        //    //                _db.Memberships.Update(membership);
        //    //                await _db.SaveChangesAsync();
        //    //                _logger.LogInformation("Membership updated successfully for member {MemberId}", existingMember.Id);
        //    //            }
        //    //            else
        //    //            {
        //    //                _logger.LogInformation("Creating new membership for member {MemberId}", existingMember.Id);

        //    //                // Create new membership
        //    //                membership = new Membership
        //    //                {
        //    //                    expiration = DateTime.Now.AddMonths(1).AddHours(1),
        //    //                    isMonthly = true,
        //    //                    isMonthlyActive = true,
        //    //                    memberID = existingMember.Id,
        //    //                    phone = transaction.PayerPhone,
        //    //                    transactions = transaction.TransactionId + ";"
        //    //                };

        //    //                _logger.LogDebug("New membership created. Expiration: {Expiration}, IsMonthly: {IsMonthly}, IsMonthlyActive: {IsMonthlyActive}",
        //    //                    membership.expiration, membership.isMonthly, membership.isMonthlyActive);

        //    //                _db.Memberships.Add(membership);
        //    //                await _db.SaveChangesAsync();
        //    //            }
        //    //        }
        //    //        else
        //    //        {
        //    //            _logger.LogError("No member ID found for transaction with email {Email}", transaction.PayerEmail);
        //    //        }

        //    //        _logger.LogInformation("Successfully completed HandleMeshulamResponse2847g93j596034");
        //    //        return Ok();
        //    //    }
        //    //    catch (Exception ex)
        //    //    {
        //    //        _logger.LogError(ex, "Error in HandleMeshulamResponse2847g93j596034: {ErrorMessage}", ex.Message);

        //    //        // Log inner exceptions if present
        //    //        Exception innerEx = ex.InnerException;
        //    //        int depth = 1;
        //    //        while (innerEx != null)
        //    //        {
        //    //            _logger.LogError("Inner exception level {Depth}: {ErrorType} - {ErrorMessage}",
        //    //                depth, innerEx.GetType().Name, innerEx.Message);
        //    //            innerEx = innerEx.InnerException;
        //    //            depth++;
        //    //        }

        //    //        // Log stack trace for debugging
        //    //        _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);

        //    //        return StatusCode(500, ex.Message);
        //    //    }
        //}
        [Route("meshulam-dd-success")]
        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> HandleMeshulamResponse2847g93j596034()
        {
            _logger.LogInformation("Starting Direct Debit Recurring Handler");

            try
            {
                var payload = await _webhookPayloadReader.ReadAsync(Request);
                if (payload.IsJson)
                {
                    _logger.LogInformation("Meshulam recurring webhook payload format=json");
                }
                else if (payload.HasForm)
                {
                    _logger.LogInformation("Meshulam recurring webhook payload format=form");
                }

                var transaction = MapSuccessWebhookTransaction(payload);
                var classification = await ClassifyHarnessAsync(payload, transaction);
                _paymentsHarnessStore.RecordRawWebhook(
                    "/meshulam-dd-success",
                    payload,
                    transaction,
                    classification,
                    Request.Method,
                    Request.QueryString.Value,
                    BuildHeadersDump(Request.Headers));
                var isHarness = classification.IsHarness;
                var persist = !isHarness;

                if (transaction == null || transaction.Sum == null)
                {
                    if (IsEmptyWebhookProbe(payload))
                    {
                        _logger.LogInformation("Ignoring empty Meshulam recurring-success callback probe. method={Method}", Request.Method);
                        if (isHarness)
                        {
                            _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "ok", "empty-probe-ignored");
                        }

                        return Ok();
                    }

                    if (isHarness)
                    {
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "failed", "invalid-payload");
                        return Ok();
                    }
                    _logger.LogError("Failed to map recurring transaction or Sum is null.");
                    return BadRequest("Invalid Payload");
                }

                if (!meshulamService.IsSuccessfulTransaction(transaction))
                {
                    if (isHarness)
                    {
                        _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "failed", "non-success-status");
                    }
                    _logger.LogWarning("Ignoring non-success recurring callback. token={TransactionToken}, status={Status}, statusCode={StatusCode}",
                        transaction.TransactionToken, transaction.Status, transaction.StatusCode);
                    return Ok();
                }

                if (string.IsNullOrWhiteSpace(transaction.TransactionToken))
                {
                    if (isHarness)
                    {
                        _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                        _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "failed", "missing-transaction-token");
                        return Ok();
                    }
                    return BadRequest("Missing transaction token");
                }

                var persistedDuplicateTransaction = isHarness
                    ? null
                    : _db.Transactions.FirstOrDefault(x => x.TransactionToken == transaction.TransactionToken);
                var duplicate = isHarness
                    ? _paymentsHarnessStore.HasSeenTransactionToken(transaction.TransactionToken)
                    : persistedDuplicateTransaction != null;
                if (duplicate && isHarness)
                {
                    _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                    _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "ok", "duplicate-ignored");
                    _logger.LogInformation("Duplicate recurring callback ignored. token={TransactionToken}", transaction.TransactionToken);
                    return Ok();
                }

                if (duplicate)
                {
                    if (!CanReconcileDuplicateTransaction(persistedDuplicateTransaction))
                    {
                        _logger.LogInformation("Duplicate recurring callback ignored outside reconciliation window. token={TransactionToken}, created={Created}",
                            transaction.TransactionToken,
                            persistedDuplicateTransaction?.Created);
                        return Ok();
                    }

                    _logger.LogInformation("Duplicate recurring callback received, resuming reconciliation. token={TransactionToken}", transaction.TransactionToken);
                }

                // Validate callback against provider API when callback contains transaction id+token.
                // Do not hard-fail recurring webhook processing if validation fails:
                // sandbox callbacks can arrive while ForceSandboxMode=false and must still persist.
                bool hasProviderIdentifiers = transaction.TransactionId.HasValue &&
                                              !string.IsNullOrWhiteSpace(transaction.TransactionToken) &&
                                              !transaction.TransactionToken.StartsWith("wh:", StringComparison.Ordinal);
                if (!duplicate && hasProviderIdentifiers)
                {
                    bool transactionExistsInProvider = await meshulamService.GetTransactionInfo(transaction);
                    if (!transactionExistsInProvider)
                    {
                        if (isHarness)
                        {
                            _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                            _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "ok", "provider-validation-failed-continued");
                        }
                        _logger.LogWarning("Recurring callback provider validation failed, continuing processing. token={TransactionToken}", transaction.TransactionToken);
                    }
                }

                // 2. Save Transaction Record
                if (persist)
                {
                    if (!duplicate)
                    {
                        _db.Transactions.Add(transaction);
                        await _db.SaveChangesAsync();
                    }
                }

                // 3. Update Membership Logic
                bool monthly = meshulamService.IsMonthlyTransaction(transaction);
                var paymentReference = ExtractPaymentReference(payload);

                // Find the member by email
                var existingMember = await ResolveMemberFromPaymentReferenceAsync(paymentReference);
                if (existingMember == null && !string.IsNullOrWhiteSpace(transaction.PayerEmail))
                {
                    existingMember = await _memberManager.FindByEmailAsync(transaction.PayerEmail);
                }
                if (existingMember == null && transaction.DirectDebitId.HasValue)
                {
                    var fallbackTransactions = _db.Transactions
                        .Where(x => x.DirectDebitId == transaction.DirectDebitId &&
                                    !string.IsNullOrWhiteSpace(x.PayerEmail) &&
                                    x.TransactionToken != transaction.TransactionToken &&
                                    (!transaction.TransactionId.HasValue || x.TransactionId != transaction.TransactionId))
                        .OrderByDescending(x => x.Created)
                        .ToList();

                    foreach (var previousDirectDebitTransaction in fallbackTransactions)
                    {
                        existingMember = await _memberManager.FindByEmailAsync(previousDirectDebitTransaction.PayerEmail);
                        if (existingMember != null)
                        {
                            break;
                        }
                    }
                }

                if (existingMember != null)
                {
                    var (paymentLinked, membershipChanged) = await EnsureMembershipForTransactionAsync(existingMember, transaction, monthly, persist);
                    if (membershipChanged)
                    {
                        _logger.LogInformation("Recurring payment linked to membership for {Email}. token={TransactionToken}", existingMember.Email, transaction.TransactionToken);
                    }
                    else if (paymentLinked)
                    {
                        _logger.LogInformation("Recurring payment was already linked to membership for {Email}. token={TransactionToken}", existingMember.Email, transaction.TransactionToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Received recurring payment but no member could be resolved. email={Email}, paymentReference={PaymentReference}, transactionToken={TransactionToken}",
                        transaction.PayerEmail,
                        paymentReference,
                        transaction.TransactionToken);
                }

                if (isHarness)
                {
                    _paymentsHarnessStore.UpdateRunFromWebhook(classification.RunId, transaction);
                    _paymentsHarnessStore.RecordHarnessEvent("/meshulam-dd-success", payload, transaction, classification, "ok", "processed-no-db");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Direct Debit Webhook");
                return StatusCode(500, ex.Message);
            }
        }
    }
    
}

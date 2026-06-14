using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using MyProject12.Models;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Security;

namespace MyProject12.Services
{
    public class MeshulamService
    {

        private readonly DB _db; // Entity Framework Core DB context
        private readonly IMemberManager _memberManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MeshulamService> _logger;


        private readonly string MeshulamBaseAddress;// = "https://sandbox.meshulam.co.il/api/light/server/1.0/";
        private readonly string CreatePaymentProcessEndpoint = "createPaymentProcess/";
        private readonly string ApproveTransactionEndpoint = "approveTransaction/";
        private readonly string GetTransactionInfoEndpoint = "getTransactionInfo/";
        private readonly string GetPaymentProcessInfoEndpoint = "getPaymentProcessInfo/";
        private readonly string RefundTransactionEndpoint = "refundTransaction/";
        private static readonly string UpdateDirectDebitEndpoint = "updateDirectDebit/";

        public static string UserID;
        public readonly string YearlyPageCode;
        public readonly string MonthlyPageCode;

        public int yearlyPrice = 348; //default value in case there is an issue
        public int monthlyPrice = 35; //default value in case there is an issue



        public MeshulamService(IPublishedContentQuery publishedContetnQuery, IContentService contentService, IMemberManager memberManager, DB db, IConfiguration configuration, ILogger<MeshulamService> logger)
        {
            
            dynamic account = publishedContetnQuery.ContentAtRoot().First().DescendantsOrSelf().FirstOrDefault(x => x.ContentType.Alias == "account");

            //dynamic account = contentService.GetRootContent().FirstOrDefault(c => c.ContentType.Alias == "account");
            if (account != null)
            {
                yearlyPrice = ((int)account.SubscriptionYearlyMonthlyPrice) * 12;
                monthlyPrice = (int)account.SubscriptionDirectDebitPrice;
            }


            _db = db;
            _configuration = configuration;
            _memberManager = memberManager;
            _logger = logger;
            var configuredAddress = _configuration["Meshulam:Address"];
            var configuredUserId = _configuration["Meshulam:UserID"];
            var configuredMonthlyPageCode = _configuration["Meshulam:MonthlyPageCode"];
            var configuredYearlyPageCode = _configuration["Meshulam:YearlyPageCode"];

            var forceSandboxMode = bool.TryParse(_configuration["Meshulam:ForceSandboxMode"], out var forceSandbox) && forceSandbox;
            var sandboxAddress = MeshulamApiUrl.NormalizeBaseAddress(
                _configuration["Meshulam:SandboxAddress"] ?? "https://sandbox.meshulam.co.il/api/light/server/1.0/");
            var sandboxUserId = _configuration["Meshulam:SandboxUserID"];
            var sandboxMonthlyPageCode = _configuration["Meshulam:SandboxMonthlyPageCode"];
            var sandboxYearlyPageCode = _configuration["Meshulam:SandboxYearlyPageCode"];

            var selectedBaseAddress = forceSandboxMode ? sandboxAddress : configuredAddress;
            MeshulamBaseAddress = MeshulamApiUrl.NormalizeBaseAddress(selectedBaseAddress);
            var useSandboxCredentials = forceSandboxMode || IsSandboxAddress(MeshulamBaseAddress);

            UserID = useSandboxCredentials && !string.IsNullOrWhiteSpace(sandboxUserId)
                ? sandboxUserId
                : configuredUserId;
            MonthlyPageCode = useSandboxCredentials && !string.IsNullOrWhiteSpace(sandboxMonthlyPageCode)
                ? sandboxMonthlyPageCode
                : configuredMonthlyPageCode;
            YearlyPageCode = useSandboxCredentials && !string.IsNullOrWhiteSpace(sandboxYearlyPageCode)
                ? sandboxYearlyPageCode
                : configuredYearlyPageCode;
        }

        private static bool IsSandboxAddress(string? baseAddress)
        {
            return !string.IsNullOrWhiteSpace(baseAddress) &&
                   baseAddress.Contains("sandbox.meshulam.co.il", StringComparison.OrdinalIgnoreCase);
        }
        public (int, string) MapCPP(string jsonString)
        {
            var jObject = JObject.Parse(jsonString);

            int status = (int)jObject["status"];
            string url = (string)jObject["data"]["url"];

            return (status, url);
        }

        public static bool MapStatus(string jsonString)
        {
            var jObject = JObject.Parse(jsonString);

            int status = (int)jObject["status"];

            return status == 1 ? true : false;
        }

        private static int? ParseNullableIntToken(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Integer)
            {
                return (int)token;
            }

            var value = token.ToString();
            return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null;
        }

        private static float? ParseNullableFloatToken(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return token.Value<float>();
            }

            var raw = token.ToString();
            if (float.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInvariant))
            {
                return parsedInvariant;
            }

            if (float.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("he-IL"), out var parsedHe))
            {
                return parsedHe;
            }

            return null;
        }

        private static string BuildWebhookSyntheticToken(JToken root)
        {
            var parts = new[]
            {
                (string)root["transactionCode"] ?? "",
                (string)root["directDebitId"] ?? "",
                (string)root["paymentDate"] ?? "",
                (string)root["paymentSum"] ?? "",
                (string)root["payerEmail"] ?? (string)root["email"] ?? ""
            };

            var joined = string.Join("|", parts);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            return "wh:" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static int? MapPaymentTypeFromText(string value)
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

        public bool IsSuccessfulTransaction(Transaction transaction)
        {
            if (transaction == null)
            {
                return false;
            }

            return transaction.StatusCode == 2 ||
                   string.Equals(transaction.Status, "שולם", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsMonthlyTransaction(Transaction transaction)
        {
            if (transaction == null)
            {
                return false;
            }

            if (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0)
            {
                return true;
            }

            if (transaction.PaymentType.HasValue)
            {
                return transaction.PaymentType.Value == 1;
            }

            if (transaction.Sum.HasValue)
            {
                // Amounts can be sent as float; use tolerance and only as fallback.
                return Math.Abs(transaction.Sum.Value - monthlyPrice) < 0.01f;
            }

            return false;
        }

        public bool IsExpectedMembershipAmount(Transaction transaction)
        {
            if (transaction?.Sum == null)
            {
                return false;
            }

            return Math.Abs(transaction.Sum.Value - monthlyPrice) < 0.01f ||
                   Math.Abs(transaction.Sum.Value - yearlyPrice) < 0.01f;
        }

        private string ResolvePageCode(Transaction transaction)
        {
            return IsMonthlyTransaction(transaction) ? MonthlyPageCode : YearlyPageCode;
        }

        public Transaction MapFormDataToTransaction(IFormCollection formData)
        {
            return MeshulamTransactionMapper.MapFormDataToTransaction(formData);
        }

        private async Task<string> SendMeshulamRequest(string endpoint, Dictionary<string, string> requestData)
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(MeshulamBaseAddress) };
            var content = new FormUrlEncodedContent(requestData);
            var response = await httpClient.PostAsync(endpoint, content);
            var rawResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meshulam HTTP call returned non-success status. baseAddress={BaseAddress}, endpoint={Endpoint}, statusCode={StatusCode}, response={Response}",
                    MeshulamBaseAddress,
                    endpoint,
                    (int)response.StatusCode,
                    rawResponse);
            }

            return rawResponse;
        }

        public async Task<string> CreatePaymentProcess(
            string sum,
            string maxPaymentNum,
            string successUrl,
            string cancelUrl,
            string pageFieldEmail,
            string pageFieldFullName,
            string pageFieldPhone,
            string pageCode,
            string cField1 = null,
            string notifyUrl = null,
            string description = "MO6 Payment")
        {
            var requestData = new Dictionary<string, string>
    {
        { "userId", UserID },
        { "sum", sum },
        { "maxPaymentNum", maxPaymentNum },
        { "successUrl", successUrl },
        { "cancelUrl", cancelUrl },
        { "pageField[email]", pageFieldEmail },
        { "pageField[fullName]", pageFieldFullName },
        { "pageField[phone]", pageFieldPhone },
        { "pageCode", pageCode },
        { "description", description }
    };

            if (!string.IsNullOrWhiteSpace(notifyUrl))
            {
                requestData["notifyUrl"] = notifyUrl;
            }

            // Add cField1 if provided
            if (!string.IsNullOrEmpty(cField1))
            {
                requestData.Add("cField1", cField1);
            }

            return await SendMeshulamRequest(CreatePaymentProcessEndpoint, requestData);
        }

        public async Task<bool> ApproveTransaction(Transaction transaction)
        {
            var requestData = new Dictionary<string, string>
            {
                { "pageCode", ResolvePageCode(transaction) }, // Replace with your actual page code
                { "transactionId", transaction.TransactionId?.ToString() },
                { "transactionToken", transaction.TransactionToken },
                { "sum", transaction.Sum?.ToString(CultureInfo.InvariantCulture) },
                { "paymentSum", transaction.Sum?.ToString(CultureInfo.InvariantCulture) },
                { "firstPaymentSum", transaction.FirstPaymentSum?.ToString(CultureInfo.InvariantCulture) },
                { "periodicalPaymentSum", transaction.PeriodicalPaymentSum?.ToString(CultureInfo.InvariantCulture) },
                { "paymentsNum", transaction.PaymentsNum?.ToString() },
                { "allPaymentsNum", transaction.AllPaymentsNum?.ToString() },
                { "paymentDate", transaction.PaymentDate },
                { "asmachta", transaction.Asmachta },
                { "description", transaction.Description },
                { "fullName", transaction.FullName },
                { "payerPhone", transaction.PayerPhone },
                { "payerEmail", transaction.PayerEmail },
                { "cardSuffix", transaction.CardSuffix },
                { "cardType", transaction.CardType },
                { "cardTypeCode", transaction.CardTypeCode?.ToString() },
                { "cardBrand", transaction.CardBrand },
                { "cardBrandCode", transaction.CardBrandCode?.ToString() },
                { "cardExp", transaction.CardExp },
                { "processId", transaction.ProcessId?.ToString() },
                { "processToken", transaction.ProcessToken },
                { "status", transaction.Status },
                { "statusCode", transaction.StatusCode?.ToString() },
                { "transactionTypeId", transaction.TransactionTypeId?.ToString() },
                { "paymentType", transaction.PaymentType?.ToString() },
                { "directDebitId", transaction.DirectDebitId?.ToString() },
                // Add other transaction parameters here if needed
            };
            bool success = MapStatus(await SendMeshulamRequest(ApproveTransactionEndpoint, requestData));

            return success;
        }

        public async Task<bool> GetTransactionInfo(Transaction transaction)
        {
            var requestData = new Dictionary<string, string>
            {
                { "pageCode", ResolvePageCode(transaction) }, // Replace with your actual page code
                { "transactionId", transaction.TransactionId.ToString() },
                { "transactionToken", transaction.TransactionToken }
            };
            bool success = MapStatus(await SendMeshulamRequest(GetTransactionInfoEndpoint, requestData));

            return success;
        }

        public async Task<bool> GetPaymentProcessInfo(Transaction transaction)
        {
            var requestData = new Dictionary<string, string>
            {
                { "pageCode", ResolvePageCode(transaction) }, // Replace with your actual page code
                { "processId", transaction.ProcessId.ToString() },
                { "processToken", transaction.ProcessToken }
            };
            bool success = MapStatus(await SendMeshulamRequest(GetPaymentProcessInfoEndpoint, requestData));

            return success;
        }

        public async Task<bool> RefundTransaction(Transaction transaction, float refundSum, bool stopDirectDebit = false)
        {
            bool monthly = IsMonthlyTransaction(transaction);

            var requestData = new Dictionary<string, string>
            {
                { "pageCode", monthly ? MonthlyPageCode : YearlyPageCode }, // Replace with your actual page code
                { "transactionId", transaction.TransactionId.ToString() },
                { "transactionToken", transaction.TransactionToken },
                { "refundSum", refundSum.ToString(CultureInfo.InvariantCulture) },
                //{ "stopDirectDebit", stopDirectDebit ? "1" : "0" }
            };
            if (monthly)
            {
                requestData.Add("stopDirectDebit", stopDirectDebit ? "1" : "0");
            }
            bool success = MapStatus(await SendMeshulamRequest(RefundTransactionEndpoint, requestData));

            return success;
        }

        public async Task<bool> UpdateDirectDebit(Transaction transaction, bool disableDirectDebit, string email = "")
        {
            if (transaction == null)
            {
                return false;
            }

            var parsedTransactionIds = new HashSet<int>();
            if (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0)
            {
                parsedTransactionIds.Add(transaction.TransactionId.Value);
            }

            var parsedTransactionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedSourceToken = MembershipCancellationHelper.NormalizeComparisonValue(transaction.TransactionToken);
            if (!string.IsNullOrWhiteSpace(normalizedSourceToken))
            {
                parsedTransactionTokens.Add(normalizedSourceToken);
            }

            var normalizedEmail = MembershipCancellationHelper.NormalizeComparisonValue(email);
            var cancellationMarkerToken = MembershipCancellationHelper.CancellationMarkerToken;
            var candidateTransactions = _db.Transactions
                .Where(x => x.TransactionToken == null || x.TransactionToken.Trim() != cancellationMarkerToken)
                .Where(x =>
                    (transaction.DirectDebitId.HasValue && x.DirectDebitId.HasValue && x.DirectDebitId.Value == transaction.DirectDebitId.Value) ||
                    (transaction.TransactionId.HasValue && x.TransactionId.HasValue && x.TransactionId.Value == transaction.TransactionId.Value) ||
                    (!string.IsNullOrWhiteSpace(x.TransactionToken) &&
                     parsedTransactionTokens.Contains(x.TransactionToken.Trim().ToLower())) ||
                    (!string.IsNullOrWhiteSpace(x.Asmachta) &&
                     !string.IsNullOrWhiteSpace(transaction.Asmachta) &&
                     x.Asmachta == transaction.Asmachta) ||
                    (!string.IsNullOrWhiteSpace(x.PayerEmail) &&
                     !string.IsNullOrWhiteSpace(normalizedEmail) &&
                     x.PayerEmail.Trim().ToLower() == normalizedEmail))
                .OrderByDescending(x => x.Created)
                .Take(250)
                .ToList();

            var requestTransaction = MembershipCancellationHelper.SelectBestCancellationCandidate(
                candidateTransactions,
                parsedTransactionIds,
                parsedTransactionTokens,
                email) ?? transaction;

            var requestData = new Dictionary<string, string>
            {
                { "userId", UserID },
                { "pageCode", ResolvePageCode(requestTransaction) },
                { "changeStatus", disableDirectDebit ? "2" : "1" }
            };

            if (requestTransaction.TransactionId.HasValue && requestTransaction.TransactionId.Value > 0)
            {
                requestData["transactionId"] = requestTransaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (MembershipCancellationHelper.IsProviderToken(requestTransaction.TransactionToken))
            {
                requestData["transactionToken"] = requestTransaction.TransactionToken.Trim();
            }

            if (MembershipCancellationHelper.IsProviderAsmachta(requestTransaction.Asmachta))
            {
                requestData["asmachta"] = requestTransaction.Asmachta.Trim();
            }

            if (requestTransaction.DirectDebitId.HasValue && requestTransaction.DirectDebitId.Value > 0)
            {
                requestData["directDebitId"] = requestTransaction.DirectDebitId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (!email.IsNullOrEmpty())
            {
                requestData["email"] = email;
            }

            // Keep cancellation robust even when webhook payload did not contain transactionId/transactionToken.
            if (!requestData.ContainsKey("transactionId") &&
                !requestData.ContainsKey("directDebitId") &&
                !requestData.ContainsKey("transactionToken") &&
                !requestData.ContainsKey("asmachta"))
            {
                _logger.LogWarning("UpdateDirectDebit aborted due to missing identifiers. email={Email}, asmachta={Asmachta}", email, requestTransaction.Asmachta);
                return false;
            }

            string rawResponse = await SendMeshulamRequest(UpdateDirectDebitEndpoint, requestData);
            bool success = false;
            try
            {
                var response = JObject.Parse(rawResponse);
                var status = (int?)response["status"] ?? 0;
                var err = ((string)response["err"] ?? (string)response["error"] ?? string.Empty).Trim();
                success = status == 1 && string.IsNullOrEmpty(err);

                if (success)
                {
                    var changedStatus = (string)response["data"]?["changeStatus"];
                    if (!string.IsNullOrWhiteSpace(changedStatus))
                    {
                        var allowedStatuses = disableDirectDebit
                            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "2", "0" } // observed legacy + current provider values
                            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1" };

                        if (!allowedStatuses.Contains(changedStatus))
                        {
                            _logger.LogWarning("UpdateDirectDebit returned unexpected changeStatus value. changeStatus={ChangeStatus}, disable={Disable}, response={RawResponse}",
                                changedStatus,
                                disableDirectDebit,
                                rawResponse);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateDirectDebit parse failure. rawResponse={RawResponse}", rawResponse);
                success = false;
            }

            if (!success)
            {
                _logger.LogWarning("UpdateDirectDebit failed. disable={Disable}, email={Email}, transactionId={TransactionId}, directDebitId={DirectDebitId}, response={RawResponse}",
                    disableDirectDebit,
                    email,
                    requestTransaction.TransactionId,
                    requestTransaction.DirectDebitId,
                    rawResponse);
            }

            if (success && !email.IsNullOrEmpty())
            {
                Transaction tr = new Transaction
                {
                    Created = DateTime.Now,
                    PayerEmail = email,
                    ProcessToken = MembershipCancellationHelper.CancellationMarkerToken,
                    TransactionToken = MembershipCancellationHelper.CancellationMarkerToken,
                    FullName = requestTransaction.FullName,
                    PayerPhone = requestTransaction.PayerPhone,
                    CardBrand = requestTransaction.CardBrand,
                    CardBrandCode = requestTransaction.CardBrandCode,
                    CardExp = requestTransaction.CardExp,
                    CardSuffix = requestTransaction.CardSuffix,
                    CardType = requestTransaction.CardType,
                    CardTypeCode = requestTransaction.CardTypeCode,
                    Asmachta = "",
                    AllPaymentsNum = requestTransaction.AllPaymentsNum,
                    CardToken = requestTransaction.CardToken,
                    Description = requestTransaction.Description,
                    DirectDebitId = requestTransaction.DirectDebitId,
                    FirstPaymentSum = 0,
                    PaymentDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    PaymentsNum = 0,
                    PaymentType = 0,
                    PeriodicalPaymentSum = 0,
                    ProcessId = 0,
                    Status = "",
                    StatusCode = 0,
                    Sum = 0,
                    TransactionId = 0,
                    TransactionTypeId = 0
                };
                _db.Transactions.Add(tr);
                _db.SaveChanges();
            }
            return success;
        }

        public async Task<bool> CancelDirectDebit(Transaction transaction, string email = "")
        {
            if (!MembershipCancellationHelper.IsDirectDebitCandidate(transaction) ||
                !MembershipCancellationHelper.HasRequiredUpdateDirectDebitIdentifiers(transaction))
            {
                _logger.LogWarning(
                    "CancelDirectDebit aborted due to missing provider identifiers. email={Email}, transactionId={TransactionId}, token={TransactionToken}, asmachta={Asmachta}, directDebitId={DirectDebitId}",
                    email,
                    transaction?.TransactionId,
                    transaction?.TransactionToken,
                    transaction?.Asmachta,
                    transaction?.DirectDebitId);
                return false;
            }

            var requestData = new Dictionary<string, string>
            {
                { "userId", UserID },
                { "pageCode", ResolvePageCode(transaction) },
                { "changeStatus", "2" },
                { "transactionId", transaction.TransactionId!.Value.ToString(CultureInfo.InvariantCulture) },
                { "transactionToken", transaction.TransactionToken.Trim() },
                { "asmachta", transaction.Asmachta.Trim() }
            };

            if (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0)
            {
                requestData["directDebitId"] = transaction.DirectDebitId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                requestData["email"] = email;
            }

            string rawResponse = await SendMeshulamRequest(UpdateDirectDebitEndpoint, requestData);
            bool success = false;
            try
            {
                var response = JObject.Parse(rawResponse);
                var status = (int?)response["status"] ?? 0;
                var err = ((string)response["err"] ?? (string)response["error"] ?? string.Empty).Trim();
                var changedStatus = ((string)response["data"]?["changeStatus"] ?? string.Empty).Trim();

                success = status == 1 &&
                          string.IsNullOrEmpty(err) &&
                          (string.IsNullOrWhiteSpace(changedStatus) || changedStatus == "2" || changedStatus == "0");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelDirectDebit parse failure. rawResponse={RawResponse}", rawResponse);
                success = false;
            }

            if (!success)
            {
                _logger.LogWarning(
                    "CancelDirectDebit failed. email={Email}, transactionId={TransactionId}, directDebitId={DirectDebitId}, response={RawResponse}",
                    email,
                    transaction.TransactionId,
                    transaction.DirectDebitId,
                    rawResponse);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                Transaction? cancellationTransaction = null;
                try
                {
                    cancellationTransaction = new Transaction
                    {
                        Created = DateTime.Now,
                        PayerEmail = email,
                        ProcessToken = MembershipCancellationHelper.CancellationMarkerToken,
                        TransactionToken = MembershipCancellationHelper.CancellationMarkerToken,
                        FullName = transaction.FullName,
                        PayerPhone = transaction.PayerPhone,
                        CardBrand = transaction.CardBrand,
                        CardBrandCode = transaction.CardBrandCode,
                        CardExp = transaction.CardExp,
                        CardSuffix = transaction.CardSuffix,
                        CardType = transaction.CardType,
                        CardTypeCode = transaction.CardTypeCode,
                        Asmachta = string.Empty,
                        AllPaymentsNum = transaction.AllPaymentsNum,
                        CardToken = transaction.CardToken,
                        Description = transaction.Description,
                        DirectDebitId = transaction.DirectDebitId,
                        FirstPaymentSum = 0,
                        PaymentDate = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        PaymentsNum = 0,
                        PaymentType = 0,
                        PeriodicalPaymentSum = 0,
                        ProcessId = 0,
                        Status = string.Empty,
                        StatusCode = 0,
                        Sum = 0,
                        TransactionId = 0,
                        TransactionTypeId = 0
                    };
                    _db.Transactions.Add(cancellationTransaction);
                    _db.SaveChanges();
                }
                catch (Exception ex)
                {
                    if (cancellationTransaction != null)
                    {
                        _db.Entry(cancellationTransaction).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                    }

                    _logger.LogError(
                        ex,
                        "CancelDirectDebit succeeded at provider but failed to record local cancellation marker. email={Email}, transactionId={TransactionId}, directDebitId={DirectDebitId}",
                        email,
                        transaction.TransactionId,
                        transaction.DirectDebitId);
                }
            }

            return true;
        }

        // In MeshulamService.cs

        public Transaction MapJsonToTransaction(string jsonString)
        {
            return MeshulamTransactionMapper.MapJsonToTransaction(jsonString);
        }
    }
}

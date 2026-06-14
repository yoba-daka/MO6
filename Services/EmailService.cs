using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MO6.Models;
using MosheSharon;
using MyProject12.Models;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using static Lucene.Net.Queries.Function.ValueSources.MultiFunction;

namespace MyProject12.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IContentService _contentService;
        private readonly AmazonSimpleEmailServiceClient _client;
        private readonly string root, senderEmail;
        private readonly DB _db;

        public EmailService(IConfiguration configuration, IContentService contentService, DB db)
        {
            _configuration = configuration;
            _contentService = contentService;
            _db = db;
            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];
            var awsRegion = _configuration["AWS:Region"];
            root = NormalizeBaseUrl(_configuration["CustomDomain"] ?? _configuration["AWS:RootUrl"]);
            senderEmail = _configuration["AWS:SenderEmail"];

            var regionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);

            _client = new AmazonSimpleEmailServiceClient(awsAccessKey, awsSecretKey, regionEndpoint);
        }

        public async Task SendHtmlEmail(string toAddress, string subject, string htmlBody, string sender = "")
        {
            sender = sender == "" ? senderEmail : sender;
            var sendRequest = new SendEmailRequest
            {
                Source = $"Moshe Sharon<{sender}>",
                Destination = new Destination { ToAddresses = new List<string> { toAddress } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Html = new Content(htmlBody) }
                }
            };

            await _client.SendEmailAsync(sendRequest);
        }

        public async Task GenerateSendVerificationEmail(string name, string email, IUrlHelper Url)
        {
            // Send verification email
            await _db.EmailVerifications.Where(x => x.Email == email).ExecuteDeleteAsync();
            EmailVerification ev = new EmailVerification { Code = Helpers.GenerateUniqueCode(8), Created = DateTime.Now, Email = email, TimesSent = 0 };
            _db.EmailVerifications.Add(ev);
            await _db.SaveChangesAsync();


            var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });


            await this.SendVerificationEmail(name, email, ev.Code, link);

        }

        public async Task GenerateSendVerificationEmail(string name, string email)
        {
            await _db.EmailVerifications.Where(x => x.Email == email).ExecuteDeleteAsync();
            EmailVerification ev = new EmailVerification { Code = Helpers.GenerateUniqueCode(8), Created = DateTime.Now, Email = email, TimesSent = 0 };
            _db.EmailVerifications.Add(ev);
            await _db.SaveChangesAsync();

            var link = $"/verification?email={Uri.EscapeDataString(ev.Email)}&code={Uri.EscapeDataString(ev.Code)}";
            await SendVerificationEmail(name, email, ev.Code, link);
        }

        public async Task SendVerificationNewMemberEmail(string name, string email, string code, string link)
        {
            string messageBody = "";

            var content = _contentService.GetById(3738);
            var rawContent = content?.GetValue<string>("NewMembershipEmail") ?? "";

            messageBody = ExtractContentFromRTE(rawContent);

            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>הודעת מערכת</h2>
                <p><strong>שלום {name}!</strong></p>
                {messageBody}
                <br />
                <h2 style='color: #f77b26;'>אימות כתובת אימייל</h2>
                <a href='{BuildAbsoluteUrl(link)}' style='background-color: #f77b26; color: #ffffff; padding: 10px 15px; text-decoration: none; border-radius: 4px;'>נא ללחוץ כאן לאימות</a>
                <p>קוד האימות שלך הוא: <strong>{code}</strong></p>
                <p>הלינק והקוד תקפים ל24 שעות בלבד</p>
            </div>";

            await SendHtmlEmail(email, "אימות מנוי חדש", emailContent);
        }

        public async Task SendVerificationEmail(string name, string email, string code, string link)
        {
            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>אימות כתובת אימייל</h2>
                <p>שלום {name}!</p>
                <a href='{BuildAbsoluteUrl(link)}' style='background-color: #f77b26; color: #ffffff; padding: 10px 15px; text-decoration: none; border-radius: 4px;'>נא ללחוץ כאן לאימות</a>
                <p>קוד האימות שלך הוא: <strong>{code}</strong></p>
                <p>הלינק והקוד תקפים ל24 שעות בלבד</p>
            </div>";

            await SendHtmlEmail(email, "אימות חשבון חדש", emailContent);
        }

        public async Task SendManagementNewMemberEmail(string name, string newMemberEmail, string email, string[] args = null)
        {
            string conArgs = string.Concat(args.Select(x=>$@"<p>{x}</p>"));
            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>משתמש חדש</h2>
                <p>{name}</p>
                <p>{newMemberEmail}</p>
                {conArgs}
            </div>";

            await SendHtmlEmail(email, "נרשם משתמש חדש", emailContent);
        }

        public async Task SendManagementNewMembershipEmail(string name, string newMemberEmail, bool isMonthly, string email, string phone = "")
        {
            string normalizedPhone = string.IsNullOrWhiteSpace(phone) ? "לא נמסר" : phone.Trim();
            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>מנוי חדש</h2>
                <p>{name}</p>
                <p>{newMemberEmail}</p>
                <p>מנוי {(isMonthly ? "חודשי מתחדש" : "שנתי")}</p>
                <p>פלאפון: {normalizedPhone}</p>
            </div>";

            await SendHtmlEmail(email, "נוצר מנוי חדש", emailContent);
        }


        public async Task SendChangeEmailEmail(string name, string email, string code, string link)
        {
            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>אימות כתובת אימייל חדשה</h2>
                <p>שלום {name}!</p>
                <a href='{BuildAbsoluteUrl(link)}' style='background-color: #f77b26; color: #ffffff; padding: 10px 15px; text-decoration: none; border-radius: 4px;'>נא ללחוץ כאן לאימות</a>
                <p>הלינק תקף ל24 שעות בלבד</p>
            </div>";

            //         < p > קוד האימות שלך הוא: < strong >{ code}</ strong ></ p >


            await SendHtmlEmail(email, "כתובת האימייל שונתה", emailContent);
        }
        public async Task SendPasswordResetEmail(string name, string email, string link)
        {

            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>איפוס סיסמא</h2>
                <p>שלום {name}!</p>
                <a href='{BuildAbsoluteUrl(link)}' style='background-color: #f77b26; color: #ffffff; padding: 10px 15px; text-decoration: none; border-radius: 4px;'>נא ללחוץ כאן לאיפוס סיסמא</a>
                <p>הקישור בתוקף למשך 24 שעות.</p>
            </div>";

            await SendHtmlEmail(email, "איפוס סיסמא", emailContent);
        }


        public async Task SendNewMemberEmail(string name, string email)
        {
            var emailContent = await GetNewMemberContent(name);
            await SendHtmlEmail(email, "הודעת מערכת", emailContent,"sharon@mo6.co.il");
        }

        public async Task SendManagementHealingActionEmail(
            string actionSummary,
            Transaction transaction,
            bool isMonthly,
            bool memberCreated,
            bool membershipChanged,
            bool verificationEmailSent,
            string? verificationError = null,
            TemporaryMember? temporaryMember = null)
        {
            var recipients = GetHealingManagementRecipients();
            if (recipients.Count == 0)
            {
                return;
            }

            string normalizedPhone = string.IsNullOrWhiteSpace(transaction?.PayerPhone) ? "לא נמסר" : WebUtility.HtmlEncode(transaction.PayerPhone.Trim());
            string tempInfo = temporaryMember == null
                ? "<p>TemporaryMember: לא שויך</p>"
                : $"<p>TemporaryMember ID: {temporaryMember.Id}</p><p>TemporaryMember Created: {temporaryMember.Created:yyyy-MM-dd HH:mm:ss}</p><p>TemporaryMember Processed: {(temporaryMember.Processed ? "כן" : "לא")}</p>";
            string verificationLine = memberCreated
                ? $"<p>אימייל אימות נשלח: {(verificationEmailSent ? "כן" : "לא")}</p>"
                : string.Empty;
            string verificationErrorLine = !string.IsNullOrWhiteSpace(verificationError)
                ? $"<p>שגיאת אימות: {WebUtility.HtmlEncode(verificationError)}</p>"
                : string.Empty;

            string emailContent = $@"
            <div dir='rtl' style='font-family: Arial, sans-serif;'>
                <h2 style='color: #f77b26;'>בוצע ריפוי אוטומטי לתשלום</h2>
                <p><strong>סיכום פעולה:</strong> {WebUtility.HtmlEncode(actionSummary)}</p>
                <p>אימייל משלם: {WebUtility.HtmlEncode(transaction?.PayerEmail ?? string.Empty)}</p>
                <p>שם מלא: {WebUtility.HtmlEncode(transaction?.FullName ?? string.Empty)}</p>
                <p>פלאפון: {normalizedPhone}</p>
                <p>סוג מנוי: {(isMonthly ? "חודשי" : "שנתי")}</p>
                <p>סכום: {transaction?.Sum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}</p>
                <p>תאריך תשלום: {WebUtility.HtmlEncode(transaction?.PaymentDate ?? string.Empty)}</p>
                <p>אסמכתא: {WebUtility.HtmlEncode(transaction?.Asmachta ?? string.Empty)}</p>
                <p>DirectDebitId: {transaction?.DirectDebitId?.ToString() ?? string.Empty}</p>
                <p>TransactionId: {transaction?.TransactionId?.ToString() ?? string.Empty}</p>
                <p>TransactionToken: {WebUtility.HtmlEncode(transaction?.TransactionToken ?? string.Empty)}</p>
                <p>נוצר משתמש: {(memberCreated ? "כן" : "לא")}</p>
                <p>המנוי עודכן/נוצר: {(membershipChanged ? "כן" : "לא")}</p>
                {verificationLine}
                {verificationErrorLine}
                {tempInfo}
            </div>";

            foreach (var recipient in recipients)
            {
                await SendHtmlEmail(recipient, "ריפוי אוטומטי בוצע לתשלום", emailContent);
            }
        }

        private async Task<string> GetNewMemberContent(string name)
        {
            string messageBody = "";

            var content = _contentService.GetById(3738);
            var rawContent = content?.GetValue<string>("NewMembershipEmail") ?? "";

            messageBody = ExtractContentFromRTE(rawContent);


            return $@"
        <div dir='rtl' style='font-family: Arial, sans-serif;'>
            <h2 style='color: #f77b26;'>הודעת מערכת</h2>
            <p><strong>שלום {name}!</strong></p>
            {messageBody}
        </div>";
        }

        private string ExtractContentFromRTE(string rawContent)
        {
            try
            {
                var json = JObject.Parse(rawContent);
                var markup = json["markup"]?.ToString();

                if (!string.IsNullOrEmpty(markup))
                {
                    // Remove any leading/trailing whitespace and newlines
                    markup = Regex.Replace(markup, @"^\s+|\s+$|\n|\r", "", RegexOptions.Multiline);
                    return markup;
                }
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error parsing RTE content: {ex.Message}");
            }

            // Return the original content if parsing fails
            return rawContent;
        }

        private List<string> GetHealingManagementRecipients()
        {
            var content = _contentService.GetById(3738);
            var primary = content?.GetValue<string>("newMemberEmailToManagement") ?? string.Empty;
            var fallback = content?.GetValue<string>("newMembershipEmailToManagement") ?? string.Empty;
            var source = !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

            return source
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return string.Empty;
            }

            var trimmed = baseUrl.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "https://" + trimmed;
            }

            return trimmed.TrimEnd('/');
        }

        private string BuildAbsoluteUrl(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return root;
            }

            if (Uri.TryCreate(link, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                return link;
            }

            return root + (link.StartsWith("/") ? link : "/" + link);
        }
    }
}

using Microsoft.AspNetCore.Http;
using MyProject12.Models;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MyProject12.Services
{
    public static class MeshulamTransactionMapper
    {
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

        private static string GetStringOrEmpty(JToken root, params string[] keys)
        {
            foreach (var key in keys)
            {
                var token = root[key];
                if (token == null)
                {
                    continue;
                }

                var value = token.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static int? ParseNullableIntString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
        }

        private static float? ParseNullableFloatString(string? value)
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

        private static Transaction CreateDefaultTransaction()
        {
            return new Transaction
            {
                Created = DateTime.Now,
                Status = "",
                StatusCode = null,
                TransactionId = null,
                TransactionToken = "",
                TransactionTypeId = null,
                PaymentType = null,
                Sum = null,
                FirstPaymentSum = null,
                PeriodicalPaymentSum = null,
                PaymentsNum = null,
                AllPaymentsNum = null,
                PaymentDate = "",
                Asmachta = "",
                Description = "",
                FullName = "",
                PayerPhone = "",
                PayerEmail = "",
                CardSuffix = "",
                CardType = "",
                CardTypeCode = null,
                CardBrand = "",
                CardBrandCode = null,
                CardExp = "",
                ProcessId = null,
                ProcessToken = "",
                CardToken = "",
                DirectDebitId = null
            };
        }

        private static bool HasMeaningfulWebhookFields(Transaction transaction)
        {
            return !string.IsNullOrWhiteSpace(transaction.TransactionToken) ||
                   transaction.TransactionId.HasValue ||
                   !string.IsNullOrWhiteSpace(transaction.Asmachta) ||
                   transaction.Sum.HasValue ||
                   transaction.DirectDebitId.HasValue ||
                   !string.IsNullOrWhiteSpace(transaction.PayerEmail) ||
                   !string.IsNullOrWhiteSpace(transaction.ProcessToken);
        }

        public static Transaction? MapLoosePayloadToTransaction(MeshulamWebhookPayload payload)
        {
            if (payload == null)
            {
                return null;
            }

            var transaction = CreateDefaultTransaction();

            var paymentTypeRaw = payload.GetValue("data[paymentType]", "paymentType");
            transaction.Status = payload.GetValue("data[status]", "status") ?? transaction.Status;
            transaction.StatusCode = ParseNullableIntString(payload.GetValue("data[statusCode]", "statusCode"));
            transaction.TransactionId = ParseNullableIntString(payload.GetValue("data[transactionId]", "transactionId", "transactionCode"));
            transaction.TransactionToken = payload.GetValue("data[transactionToken]", "transactionToken") ?? transaction.TransactionToken;
            transaction.TransactionTypeId = ParseNullableIntString(payload.GetValue("data[transactionTypeId]", "transactionTypeId"));
            transaction.PaymentType = ParseNullableIntString(paymentTypeRaw) ?? MapPaymentTypeFromText(paymentTypeRaw);
            transaction.Sum = ParseNullableFloatString(payload.GetValue("data[sum]", "sum", "paymentSum"));
            transaction.FirstPaymentSum = ParseNullableFloatString(payload.GetValue("data[firstPaymentSum]", "firstPaymentSum"));
            transaction.PeriodicalPaymentSum = ParseNullableFloatString(payload.GetValue("data[periodicalPaymentSum]", "periodicalPaymentSum"));
            transaction.PaymentsNum = ParseNullableIntString(payload.GetValue("data[paymentsNum]", "paymentsNum"));
            transaction.AllPaymentsNum = ParseNullableIntString(payload.GetValue("data[allPaymentsNum]", "allPaymentsNum", "allPaymentNum"));
            transaction.PaymentDate = payload.GetValue("data[paymentDate]", "paymentDate") ?? transaction.PaymentDate;
            transaction.Asmachta = payload.GetValue("data[asmachta]", "asmachta", "transactionCode") ?? transaction.Asmachta;
            transaction.Description = payload.GetValue("data[description]", "description", "paymentDesc") ?? transaction.Description;
            transaction.FullName = payload.GetValue("data[fullName]", "fullName", "customerName", "payer_name") ?? transaction.FullName;
            transaction.PayerPhone = payload.GetValue("data[payerPhone]", "payerPhone", "phone") ?? transaction.PayerPhone;
            transaction.PayerEmail = payload.GetValue("data[payerEmail]", "payerEmail", "email", "customerEmail") ?? transaction.PayerEmail;
            transaction.CardSuffix = payload.GetValue("data[cardSuffix]", "cardSuffix") ?? transaction.CardSuffix;
            transaction.CardType = payload.GetValue("data[cardType]", "cardType") ?? transaction.CardType;
            transaction.CardTypeCode = ParseNullableIntString(payload.GetValue("data[cardTypeCode]", "cardTypeCode"));
            transaction.CardBrand = payload.GetValue("data[cardBrand]", "cardBrand") ?? transaction.CardBrand;
            transaction.CardBrandCode = ParseNullableIntString(payload.GetValue("data[cardBrandCode]", "cardBrandCode"));
            transaction.CardExp = payload.GetValue("data[cardExp]", "cardExp") ?? transaction.CardExp;
            transaction.ProcessId = ParseNullableIntString(payload.GetValue("data[processId]", "processId"));
            transaction.ProcessToken = payload.GetValue("data[processToken]", "processToken", "Process", "webhookKey") ?? transaction.ProcessToken;
            transaction.CardToken = payload.GetValue("data[cardToken]", "cardToken") ?? transaction.CardToken;
            transaction.DirectDebitId = ParseNullableIntString(payload.GetValue("data[directDebitId]", "directDebitId", "directDebit", "DirectDebit"));

            if (string.IsNullOrWhiteSpace(transaction.TransactionToken) && !string.IsNullOrWhiteSpace(transaction.Asmachta))
            {
                using var sha = SHA256.Create();
                var input = $"{transaction.Asmachta}|{transaction.PaymentDate}|{transaction.Sum}|{transaction.PayerEmail}";
                transaction.TransactionToken = "wh:" + Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
            }

            if (transaction.StatusCode == null && (!string.IsNullOrWhiteSpace(transaction.Asmachta) || transaction.DirectDebitId.HasValue))
            {
                transaction.StatusCode = 2;
            }

            if (string.IsNullOrWhiteSpace(transaction.Status) && transaction.StatusCode == 2)
            {
                transaction.Status = "שולם";
            }

            return HasMeaningfulWebhookFields(transaction) ? transaction : null;
        }

        public static Transaction MapFormDataToTransaction(IFormCollection formData)
        {
            var transaction = CreateDefaultTransaction();

            foreach (var formField in formData)
            {
                switch (formField.Key.ToString())
                {
                    case "data[asmachta]":
                        transaction.Asmachta = formField.Value;
                        break;
                    case "data[cardSuffix]":
                        transaction.CardSuffix = formField.Value;
                        break;
                    case "data[cardType]":
                        transaction.CardType = formField.Value;
                        break;
                    case "data[cardTypeCode]":
                        transaction.CardTypeCode = int.TryParse(formField.Value, out int cardTypeCode) ? cardTypeCode : (int?)null;
                        break;
                    case "data[cardBrand]":
                        transaction.CardBrand = formField.Value;
                        break;
                    case "data[cardBrandCode]":
                        transaction.CardBrandCode = int.TryParse(formField.Value, out int cardBrandCode) ? cardBrandCode : (int?)null;
                        break;
                    case "data[cardExp]":
                        transaction.CardExp = formField.Value;
                        break;
                    case "data[firstPaymentSum]":
                        transaction.FirstPaymentSum = float.TryParse(formField.Value, out float firstPaymentSum) ? firstPaymentSum : (float?)null;
                        break;
                    case "data[periodicalPaymentSum]":
                        transaction.PeriodicalPaymentSum = float.TryParse(formField.Value, out float periodicalPaymentSum) ? periodicalPaymentSum : (float?)null;
                        break;
                    case "data[status]":
                        transaction.Status = formField.Value;
                        break;
                    case "data[statusCode]":
                        transaction.StatusCode = int.TryParse(formField.Value, out int statusCode) ? statusCode : (int?)null;
                        break;
                    case "data[transactionTypeId]":
                        transaction.TransactionTypeId = int.TryParse(formField.Value, out int transactionTypeId) ? transactionTypeId : (int?)null;
                        break;
                    case "data[paymentType]":
                        transaction.PaymentType = int.TryParse(formField.Value, out int paymentType) ? paymentType : (int?)null;
                        break;
                    case "data[sum]":
                        transaction.Sum = float.TryParse(formField.Value, out float sum) ? sum : (float?)null;
                        break;
                    case "data[paymentsNum]":
                        transaction.PaymentsNum = int.TryParse(formField.Value, out int paymentsNum) ? paymentsNum : (int?)null;
                        break;
                    case "data[allPaymentsNum]":
                        transaction.AllPaymentsNum = int.TryParse(formField.Value, out int allPaymentsNum) ? allPaymentsNum : (int?)null;
                        break;
                    case "data[allPaymentNum]":
                        transaction.AllPaymentsNum = int.TryParse(formField.Value, out int allPaymentNum) ? allPaymentNum : (int?)null;
                        break;
                    case "data[paymentDate]":
                        transaction.PaymentDate = formField.Value;
                        break;
                    case "data[description]":
                        transaction.Description = formField.Value;
                        break;
                    case "data[fullName]":
                        transaction.FullName = formField.Value;
                        break;
                    case "data[payerPhone]":
                        transaction.PayerPhone = formField.Value;
                        break;
                    case "data[payerEmail]":
                        transaction.PayerEmail = formField.Value;
                        break;
                    case "data[processId]":
                        transaction.ProcessId = int.TryParse(formField.Value, out int processId) ? processId : (int?)null;
                        break;
                    case "data[processToken]":
                        transaction.ProcessToken = formField.Value;
                        break;
                    case "data[transactionId]":
                        transaction.TransactionId = int.TryParse(formField.Value, out int transactionId) ? transactionId : (int?)null;
                        break;
                    case "data[transactionToken]":
                        transaction.TransactionToken = formField.Value;
                        break;
                    case "data[directDebitId]":
                        transaction.DirectDebitId = int.TryParse(formField.Value, out int directDebitId) ? directDebitId : (int?)null;
                        break;
                    case "transactionCode":
                        transaction.Asmachta = formField.Value;
                        break;
                    case "paymentSum":
                        transaction.Sum = float.TryParse(formField.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float paymentSum) ? paymentSum : (float?)null;
                        break;
                    case "allPaymentNum":
                        transaction.AllPaymentsNum = int.TryParse(formField.Value, out int allPaymentNumWebhook) ? allPaymentNumWebhook : (int?)null;
                        break;
                    case "paymentsNum":
                        transaction.PaymentsNum = int.TryParse(formField.Value, out int paymentsNumWebhook) ? paymentsNumWebhook : (int?)null;
                        break;
                    case "paymentType":
                        transaction.PaymentType = int.TryParse(formField.Value, out int paymentTypeWebhook) ? paymentTypeWebhook : MapPaymentTypeFromText(formField.Value);
                        break;
                    case "status":
                        transaction.Status = formField.Value;
                        break;
                    case "statusCode":
                        transaction.StatusCode = int.TryParse(formField.Value, out int statusCodeWebhook) ? statusCodeWebhook : (int?)null;
                        break;
                    case "paymentDate":
                        transaction.PaymentDate = formField.Value;
                        break;
                    case "payerEmail":
                    case "email":
                        transaction.PayerEmail = formField.Value;
                        break;
                    case "payerPhone":
                    case "phone":
                        transaction.PayerPhone = formField.Value;
                        break;
                    case "fullName":
                    case "customerName":
                        transaction.FullName = formField.Value;
                        break;
                    case "transactionId":
                        transaction.TransactionId = int.TryParse(formField.Value, out int transactionIdWebhook) ? transactionIdWebhook : (int?)null;
                        break;
                    case "transactionToken":
                        transaction.TransactionToken = formField.Value;
                        break;
                    case "directDebitId":
                        transaction.DirectDebitId = int.TryParse(formField.Value, out int directDebitIdWebhook) ? directDebitIdWebhook : (int?)null;
                        break;
                    case "paymentDesc":
                        transaction.Description = formField.Value;
                        break;
                    case "webhookKey":
                        transaction.ProcessToken = formField.Value;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(transaction.TransactionToken) && !string.IsNullOrWhiteSpace(transaction.Asmachta))
            {
                using var sha = SHA256.Create();
                var input = $"{transaction.Asmachta}|{transaction.PaymentDate}|{transaction.Sum}|{transaction.PayerEmail}";
                transaction.TransactionToken = "wh:" + Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
            }

            if (transaction.StatusCode == null && (!string.IsNullOrWhiteSpace(transaction.Asmachta) || transaction.DirectDebitId.HasValue))
            {
                transaction.StatusCode = 2;
            }
            if (string.IsNullOrWhiteSpace(transaction.Status) && transaction.StatusCode == 2)
            {
                transaction.Status = "שולם";
            }

            return transaction;
        }

        public static Transaction MapJsonToTransaction(string jsonString)
        {
            var jObject = JObject.Parse(jsonString);
            JToken root = jObject["data"] != null ? jObject["data"] : jObject;

            var paymentTypeFromText = MapPaymentTypeFromText((string)root["paymentType"]);
            var transactionToken = (string)root["transactionToken"];
            if (string.IsNullOrWhiteSpace(transactionToken))
            {
                transactionToken = BuildWebhookSyntheticToken(root);
            }

            var status = GetStringOrEmpty(root, "status", "data[status]");
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "שולם";
            }

            return new Transaction
            {
                Created = DateTime.Now,
                Status = status,
                Sum = ParseNullableFloatToken(root["sum"]) ?? ParseNullableFloatToken(root["paymentSum"]),
                TransactionId = ParseNullableIntToken(root["transactionId"]) ?? ParseNullableIntToken(root["transactionCode"]),
                TransactionToken = transactionToken,
                PayerEmail = GetStringOrEmpty(root, "payerEmail", "email", "customerEmail"),
                PayerPhone = GetStringOrEmpty(root, "payerPhone", "phone"),
                FullName = GetStringOrEmpty(root, "fullName", "customerName"),
                Asmachta = GetStringOrEmpty(root, "asmachta", "transactionCode"),
                Description = GetStringOrEmpty(root, "description", "paymentDesc"),
                ProcessToken = GetStringOrEmpty(root, "processToken", "webhookKey"),
                PaymentDate = GetStringOrEmpty(root, "paymentDate"),
                CardSuffix = GetStringOrEmpty(root, "cardSuffix"),
                CardType = GetStringOrEmpty(root, "cardType"),
                CardBrand = GetStringOrEmpty(root, "cardBrand"),
                CardExp = GetStringOrEmpty(root, "cardExp"),
                PaymentsNum = ParseNullableIntToken(root["paymentsNum"]) ?? 1,
                AllPaymentsNum = ParseNullableIntToken(root["allPaymentsNum"]) ?? ParseNullableIntToken(root["allPaymentNum"]) ?? 1,
                DirectDebitId = ParseNullableIntToken(root["directDebitId"]),
                StatusCode = ParseNullableIntToken(root["statusCode"]) ?? 2,
                PaymentType = ParseNullableIntToken(root["paymentType"]) ?? paymentTypeFromText,
                CardToken = GetStringOrEmpty(root, "cardToken")
            };
        }
    }
}

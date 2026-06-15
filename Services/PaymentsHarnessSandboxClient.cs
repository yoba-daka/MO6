using Newtonsoft.Json.Linq;
using System.Globalization;
using MyProject12.Models;
using Microsoft.Extensions.Configuration;

namespace MyProject12.Services
{
    public sealed class HarnessCreatePaymentResult
    {
        public bool Success { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
        public string RawRequest { get; set; } = string.Empty;
    }

    public sealed class HarnessCancelResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }

    public sealed class PaymentsHarnessSandboxClient
    {
        private const string DefaultSandboxBaseAddress = "https://sandbox.meshulam.co.il/api/light/server/1.0/";
        private const string DefaultSandboxUserId = "530c0ed0c411ce71";
        private const string DefaultSandboxYearlyPageCode = "a54f4954c06f";
        private const string DefaultSandboxMonthlyPageCode = "0614c4d8b7a0";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseAddress;
        private readonly string _sandboxUserId;
        private readonly string _sandboxYearlyPageCode;
        private readonly string _sandboxMonthlyPageCode;

        public PaymentsHarnessSandboxClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _baseAddress = MeshulamApiUrl.NormalizeBaseAddress(configuration["Meshulam:SandboxAddress"] ?? DefaultSandboxBaseAddress);
            _sandboxUserId = configuration["Meshulam:SandboxUserID"] ?? DefaultSandboxUserId;
            _sandboxYearlyPageCode = configuration["Meshulam:SandboxYearlyPageCode"] ?? DefaultSandboxYearlyPageCode;
            _sandboxMonthlyPageCode = configuration["Meshulam:SandboxMonthlyPageCode"] ?? DefaultSandboxMonthlyPageCode;
        }

        public async Task<HarnessCreatePaymentResult> CreatePaymentProcessAsync(
            bool monthly,
            float amount,
            string baseUrl,
            string cField1,
            string harnessToken)
        {
            var harnessEmail = $"harness{Guid.NewGuid():N}".Substring(0, 20) + "@mo6.co.il";
            var tokenQuery = Uri.EscapeDataString(harnessToken ?? string.Empty);
            var requestData = new Dictionary<string, string>
            {
                { "userId", _sandboxUserId },
                { "sum", amount.ToString(CultureInfo.InvariantCulture) },
                { "maxPaymentNum", "1" },
                { "successUrl", $"{baseUrl}/payments-harness?token={tokenQuery}" },
                { "cancelUrl", $"{baseUrl}/payments-harness?token={tokenQuery}" },
                { "notifyUrl", $"{baseUrl}/meshulam-response" },
                { "description", monthly ? "MO6 Harness Monthly Test" : "MO6 Harness Yearly Test" },
                { "pageField[email]", harnessEmail },
                { "pageField[fullName]", "Test User" },
                { "pageField[phone]", "0500000000" },
                { "pageCode", monthly ? _sandboxMonthlyPageCode : _sandboxYearlyPageCode },
                { "cField1", cField1 }
            };

            var rawRequest = string.Join("&", requestData.Select(kv => $"{kv.Key}={kv.Value}"));
            var raw = await SendFormRequestAsync("createPaymentProcess/", requestData);
            var parsed = ParseCreatePaymentResponse(raw);
            parsed.RawRequest = rawRequest;
            return parsed;
        }

        public async Task<HarnessCancelResult> UpdateDirectDebitAsync(
            int transactionId,
            string transactionToken,
            string asmachta,
            int? directDebitId,
            string email)
        {
            var requestData = new Dictionary<string, string>
            {
                { "userId", _sandboxUserId },
                { "pageCode", _sandboxMonthlyPageCode },
                { "transactionId", transactionId.ToString(CultureInfo.InvariantCulture) },
                { "transactionToken", transactionToken ?? string.Empty },
                { "asmachta", asmachta ?? string.Empty },
                { "changeStatus", "2" }
            };

            if (directDebitId.HasValue)
            {
                requestData["directDebitId"] = directDebitId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                requestData["email"] = email;
            }

            var raw = await SendFormRequestAsync("updateDirectDebit/", requestData);
            return ParseCancelResponse(raw);
        }

        public async Task<bool> IsSandboxTransactionAsync(Transaction transaction)
        {
            if (transaction == null ||
                !transaction.TransactionId.HasValue ||
                string.IsNullOrWhiteSpace(transaction.TransactionToken))
            {
                return false;
            }

            var yearlyRequest = new Dictionary<string, string>
            {
                { "pageCode", _sandboxYearlyPageCode },
                { "transactionId", transaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture) },
                { "transactionToken", transaction.TransactionToken }
            };

            var monthlyRequest = new Dictionary<string, string>
            {
                { "pageCode", _sandboxMonthlyPageCode },
                { "transactionId", transaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture) },
                { "transactionToken", transaction.TransactionToken }
            };

            try
            {
                var yearlyRaw = await SendFormRequestAsync("getTransactionInfo/", yearlyRequest);
                if (IsStatusSuccess(yearlyRaw))
                {
                    return true;
                }

                var monthlyRaw = await SendFormRequestAsync("getTransactionInfo/", monthlyRequest);
                return IsStatusSuccess(monthlyRaw);
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> SendFormRequestAsync(string endpoint, Dictionary<string, string> requestData)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_baseAddress);

            using var response = await client.PostAsync(endpoint, new FormUrlEncodedContent(requestData));
            return await response.Content.ReadAsStringAsync();
        }

        private static HarnessCreatePaymentResult ParseCreatePaymentResponse(string raw)
        {
            var result = new HarnessCreatePaymentResult { RawResponse = raw ?? string.Empty };

            try
            {
                var json = JObject.Parse(raw);
                var status = ParseInt(json["status"]);
                var err = ReadTokenAsString(json["err"]).Trim();
                var url = ReadTokenAsString(json["data"]?["url"]);
                result.Success = status == 1 && string.IsNullOrWhiteSpace(err) && !string.IsNullOrWhiteSpace(url);
                result.CheckoutUrl = url;
                result.Error = result.Success
                    ? string.Empty
                    : $"createPaymentProcess failed (status={status}, err='{err}', raw='{TrimForError(raw)}')";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private static HarnessCancelResult ParseCancelResponse(string raw)
        {
            var result = new HarnessCancelResult { RawResponse = raw ?? string.Empty };

            try
            {
                var parsed = MeshulamDirectDebitResponseParser.ParseUpdateDirectDebit(raw, disableDirectDebit: true);
                result.Success = parsed.IsStrictSuccess;
                result.Error = result.Success
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(parsed.Err)
                        ? $"updateDirectDebit failed ({parsed.FailureSummary})"
                        : parsed.Err;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private static bool IsStatusSuccess(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            try
            {
                var json = JObject.Parse(raw);
                var status = ParseInt(json["status"]);
                var err = ReadTokenAsString(json["err"], json["error"]).Trim();
                return status == 1 && string.IsNullOrWhiteSpace(err);
            }
            catch
            {
                return false;
            }
        }

        private static int ParseInt(JToken? token)
        {
            if (token == null)
            {
                return 0;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            var raw = token.ToString();
            return int.TryParse(raw, out var parsed) ? parsed : 0;
        }

        private static string ReadTokenAsString(params JToken?[] tokens)
        {
            foreach (var token in tokens)
            {
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                if (token.Type == JTokenType.String)
                {
                    var s = token.Value<string>();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                    continue;
                }

                var raw = token.ToString(Newtonsoft.Json.Formatting.None);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }

            return string.Empty;
        }

        private static string TrimForError(string? raw, int maxLen = 400)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            if (raw.Length <= maxLen)
            {
                return raw;
            }

            return raw.Substring(0, maxLen) + "...";
        }
    }
}

using Newtonsoft.Json.Linq;

namespace MyProject12.Services
{
    public sealed class MeshulamDirectDebitParseResult
    {
        public int Status { get; init; }
        public string Err { get; init; } = string.Empty;
        public string ChangeStatus { get; init; } = string.Empty;
        public bool ProviderSuccess { get; init; }
        public bool IsExpectedChangeStatus { get; init; }
        public bool IsStrictSuccess => ProviderSuccess && IsExpectedChangeStatus;

        public string FailureSummary =>
            $"status={Status}; err={Err}; changeStatus={ChangeStatus}";
    }

    public static class MeshulamDirectDebitResponseParser
    {
        public static MeshulamDirectDebitParseResult ParseUpdateDirectDebit(string? rawResponse, bool disableDirectDebit)
        {
            var response = JObject.Parse(rawResponse ?? string.Empty);
            var status = (int?)response["status"] ?? 0;
            var err = ReadTokenAsString(response["err"], response["error"]).Trim();
            var changeStatus = ReadChangeStatus(response["data"]).Trim();
            var providerSuccess = status == 1 && string.IsNullOrEmpty(err);

            return new MeshulamDirectDebitParseResult
            {
                Status = status,
                Err = err,
                ChangeStatus = changeStatus,
                ProviderSuccess = providerSuccess,
                IsExpectedChangeStatus = IsExpectedChangeStatus(changeStatus, disableDirectDebit)
            };
        }

        private static bool IsExpectedChangeStatus(string changeStatus, bool disableDirectDebit)
        {
            if (string.IsNullOrWhiteSpace(changeStatus))
            {
                return true;
            }

            if (disableDirectDebit)
            {
                return changeStatus == "2" || changeStatus == "0";
            }

            return changeStatus == "1";
        }

        private static string ReadChangeStatus(JToken? dataToken)
        {
            if (dataToken == null || dataToken.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (dataToken.Type == JTokenType.Object)
            {
                return ReadTokenAsString(dataToken["changeStatus"]);
            }

            if (dataToken.Type == JTokenType.String)
            {
                var raw = dataToken.Value<string>()?.Trim() ?? string.Empty;
                if (raw.Length == 0)
                {
                    return string.Empty;
                }

                if (raw.StartsWith("{", StringComparison.Ordinal))
                {
                    try
                    {
                        var parsed = JObject.Parse(raw);
                        return ReadTokenAsString(parsed["changeStatus"]);
                    }
                    catch
                    {
                        return raw;
                    }
                }

                return raw;
            }

            if (dataToken is JValue)
            {
                return dataToken.ToString().Trim();
            }

            return string.Empty;
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
                    var value = token.Value<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
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
    }
}

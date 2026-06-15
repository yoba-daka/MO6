using Newtonsoft.Json.Linq;

namespace MyProject12.Services
{
    public sealed class MeshulamCreatePaymentResponse
    {
        public int Status { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
        public bool IsSuccess => Status == 1 && string.IsNullOrWhiteSpace(Error) && !string.IsNullOrWhiteSpace(Url);
    }

    public static class MeshulamCreatePaymentResponseParser
    {
        public static MeshulamCreatePaymentResponse Parse(string? raw)
        {
            var result = new MeshulamCreatePaymentResponse { RawResponse = raw ?? string.Empty };

            try
            {
                var json = JObject.Parse(raw ?? string.Empty);
                result.Status = ParseInt(json["status"]);
                result.Url = ReadUrl(json["data"]);
                result.Error = ReadError(json["err"], json["error"]);

                if (result.Status != 1 && string.IsNullOrWhiteSpace(result.Error))
                {
                    result.Error = "createPaymentProcess failed.";
                }
                else if (result.Status == 1 && string.IsNullOrWhiteSpace(result.Url))
                {
                    result.Error = "createPaymentProcess returned success without a checkout URL.";
                }
            }
            catch
            {
                result.Status = 0;
                result.Error = "createPaymentProcess returned an invalid response.";
            }

            return result;
        }

        private static string ReadError(params JToken?[] tokens)
        {
            foreach (var token in tokens)
            {
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                if (token is JObject obj)
                {
                    var message = ReadTokenAsString(obj["message"], obj["err"], obj["error"]);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }
                }

                var value = ReadTokenAsString(token);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string ReadUrl(JToken? dataToken)
        {
            return dataToken is JObject dataObject
                ? ReadTokenAsString(dataObject["url"])
                : string.Empty;
        }

        private static string ReadTokenAsString(params JToken?[] tokens)
        {
            foreach (var token in tokens)
            {
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                var value = token.Type == JTokenType.String
                    ? token.Value<string>()
                    : token.ToString(Newtonsoft.Json.Formatting.None);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static int ParseInt(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return 0;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            return int.TryParse(token.ToString(), out var value) ? value : 0;
        }
    }
}

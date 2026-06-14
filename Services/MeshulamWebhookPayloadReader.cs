using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;

namespace MyProject12.Services
{
    public class MeshulamWebhookPayloadReader
    {
        public async Task<MeshulamWebhookPayload> ReadAsync(HttpRequest request)
        {
            request.EnableBuffering();
            var extraValues = BuildExtraValues(request);
            var queryForm = BuildQueryForm(request);

            var rawBody = await ReadRawBodyAsync(request);
            var contentType = request.ContentType ?? string.Empty;
            var trimmed = rawBody?.TrimStart() ?? string.Empty;

            if (LooksLikeJson(contentType, trimmed))
            {
                var json = TryParseJson(rawBody);
                if (json != null)
                {
                    return new MeshulamWebhookPayload(MeshulamWebhookPayloadKind.Json, rawBody, contentType, null, json, extraValues);
                }
            }

            var form = await TryReadFormAsync(request);
            if (form != null)
            {
                return new MeshulamWebhookPayload(MeshulamWebhookPayloadKind.Form, rawBody, contentType, form, null, extraValues);
            }

            var parsedFromBody = TryParseUrlEncoded(rawBody);
            if (parsedFromBody != null)
            {
                return new MeshulamWebhookPayload(
                    MeshulamWebhookPayloadKind.Form,
                    rawBody,
                    contentType,
                    MeshulamWebhookPayload.ToFormCollection(parsedFromBody),
                    null,
                    extraValues);
            }

            if (queryForm != null)
            {
                return new MeshulamWebhookPayload(
                    MeshulamWebhookPayloadKind.Form,
                    rawBody,
                    contentType,
                    queryForm,
                    null,
                    extraValues);
            }

            return new MeshulamWebhookPayload(MeshulamWebhookPayloadKind.Unknown, rawBody, contentType, null, null, extraValues);
        }

        private static Dictionary<string, string> BuildExtraValues(HttpRequest request)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request?.Query == null || request.Query.Count == 0)
            {
                return map;
            }

            foreach (var pair in request.Query)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var value = pair.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    map[pair.Key] = value;
                }
            }

            return map;
        }

        private static IFormCollection BuildQueryForm(HttpRequest request)
        {
            if (request?.Query == null || request.Query.Count == 0)
            {
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in request.Query)
            {
                var key = pair.Key?.Trim();
                var value = pair.Value.ToString();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                values[key] = value;
            }

            return values.Count == 0
                ? null
                : MeshulamWebhookPayload.ToFormCollection(values);
        }

        private static bool LooksLikeJson(string contentType, string trimmedBody)
        {
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return trimmedBody.StartsWith("{", StringComparison.Ordinal);
        }

        private static async Task<string> ReadRawBodyAsync(HttpRequest request)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return rawBody;
        }

        private static JObject TryParseJson(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return null;
            }

            try
            {
                return JObject.Parse(rawBody);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<IFormCollection> TryReadFormAsync(HttpRequest request)
        {
            if (!request.HasFormContentType)
            {
                return null;
            }

            try
            {
                request.Body.Position = 0;
                var form = await request.ReadFormAsync();
                request.Body.Position = 0;
                return form;
            }
            catch
            {
                request.Body.Position = 0;
                return null;
            }
        }

        private static Dictionary<string, string> TryParseUrlEncoded(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody) || !rawBody.Contains('='))
            {
                return null;
            }

            var query = rawBody.StartsWith("?", StringComparison.Ordinal) ? rawBody : $"?{rawBody}";
            var parsed = QueryHelpers.ParseQuery(query);
            if (parsed == null || parsed.Count == 0)
            {
                return null;
            }

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Linq;

namespace MyProject12.Services
{
    public enum MeshulamWebhookPayloadKind
    {
        Unknown = 0,
        Form = 1,
        Json = 2
    }

    public sealed class MeshulamWebhookPayload
    {
        private readonly Dictionary<string, string> _values;

        public MeshulamWebhookPayload(
            MeshulamWebhookPayloadKind kind,
            string rawBody,
            string contentType,
            IFormCollection formData,
            JObject jsonData,
            IDictionary<string, string>? extraValues = null)
        {
            Kind = kind;
            RawBody = rawBody ?? string.Empty;
            ContentType = contentType ?? string.Empty;
            FormData = formData;
            JsonData = jsonData;
            _values = BuildValuesMap(formData, jsonData, extraValues);
        }

        public MeshulamWebhookPayloadKind Kind { get; }
        public string RawBody { get; }
        public string ContentType { get; }
        public IFormCollection FormData { get; }
        public JObject JsonData { get; }

        public bool IsJson => Kind == MeshulamWebhookPayloadKind.Json && JsonData != null;
        public bool HasForm => Kind == MeshulamWebhookPayloadKind.Form && FormData != null;

        public string GetValue(params string[] keys)
        {
            if (keys == null)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var normalized = NormalizeKey(key);
                if (_values.TryGetValue(normalized, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static Dictionary<string, string> BuildValuesMap(IFormCollection formData, JObject jsonData, IDictionary<string, string>? extraValues)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (formData != null)
            {
                foreach (var pair in formData)
                {
                    var value = pair.Value.ToString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    map[NormalizeKey(pair.Key)] = value;
                }
            }

            if (jsonData != null)
            {
                FlattenJson(jsonData, string.Empty, map);
            }

            if (extraValues != null)
            {
                foreach (var pair in extraValues)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    {
                        continue;
                    }

                    var normalizedKey = NormalizeKey(pair.Key);
                    if (string.IsNullOrWhiteSpace(normalizedKey) || map.ContainsKey(normalizedKey))
                    {
                        continue;
                    }

                    map[normalizedKey] = pair.Value;
                }
            }

            return map;
        }

        private static void FlattenJson(JToken token, string path, IDictionary<string, string> map)
        {
            if (token == null)
            {
                return;
            }

            if (token is JValue valueToken)
            {
                var value = valueToken.ToString();
                if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(path))
                {
                    map[NormalizeKey(path)] = value;
                }
                return;
            }

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var childPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                    FlattenJson(property.Value, childPath, map);
                }
                return;
            }

            if (token is JArray arr)
            {
                for (var i = 0; i < arr.Count; i++)
                {
                    var childPath = $"{path}[{i}]";
                    FlattenJson(arr[i], childPath, map);
                }
            }
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var converted = key.Trim().Replace("][", ".").Replace("[", ".").Replace("]", string.Empty);
            converted = Regex.Replace(converted, @"\.+", ".");
            return converted.Trim('.');
        }

        public static IFormCollection ToFormCollection(IDictionary<string, string> values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var dict = values.ToDictionary(
                x => x.Key,
                x => new StringValues(x.Value),
                StringComparer.OrdinalIgnoreCase);
            return new FormCollection(dict);
        }
    }
}

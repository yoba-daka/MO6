using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class CapturedRequestsReplayIntegrationTests
{
    private sealed class CapturedRequest
    {
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Replay_CapturedWebhookRequests_ShouldParseWithUnifiedReader_WithoutDatabase()
    {
        var captured = LoadCapturedRequests("../../requests.txt");
        captured.Should().NotBeEmpty();

        var reader = new MeshulamWebhookPayloadReader();

        foreach (var req in captured)
        {
            var httpRequest = BuildRequest(req.Method, req.Path, req.ContentType, req.Body);
            var payload = await reader.ReadAsync(httpRequest);

            payload.Should().NotBeNull();
            payload.Kind.Should().Be(MeshulamWebhookPayloadKind.Form, $"captured request {req.Path} is form-urlencoded");

            if (string.Equals(req.Path, "/meshulam-dd-success", StringComparison.OrdinalIgnoreCase))
            {
                payload.GetValue("data[transactionToken]", "transactionToken").Should().NotBeNullOrWhiteSpace();
                payload.GetValue("data[directDebitId]", "directDebitId").Should().NotBeNullOrWhiteSpace();
                payload.GetValue("data[payerEmail]", "payerEmail", "email").Should().NotBeNullOrWhiteSpace();
                payload.GetValue("data[paymentType]", "paymentType").Should().NotBeNullOrWhiteSpace();
            }
            else if (string.Equals(req.Path, "/meshulam-response", StringComparison.OrdinalIgnoreCase))
            {
                payload.GetValue("data[transactionToken]", "transactionToken").Should().NotBeNullOrWhiteSpace();
                payload.GetValue("data[payerEmail]", "payerEmail", "email").Should().NotBeNullOrWhiteSpace();
                payload.GetValue("data[paymentType]", "paymentType").Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    private static HttpRequest BuildRequest(string method, string path, string contentType, string body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.ContentType = contentType;

        var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        return ctx.Request;
    }

    private static List<CapturedRequest> LoadCapturedRequests(string relativePathFromTests)
    {
        var baseDir = AppContext.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePathFromTests));
        if (!File.Exists(fullPath))
        {
            // Walk up from test output folder until we find requests.txt.
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "requests.txt");
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    break;
                }
                dir = dir.Parent;
            }
        }

        var raw = File.ReadAllText(fullPath);
        return ParseManyJsonObjects(raw)
            .Select(j => new CapturedRequest
            {
                Method = (string?)j["Method"] ?? "POST",
                Path = (string?)j["Path"] ?? string.Empty,
                ContentType = (string?)j["ContentType"] ?? "application/x-www-form-urlencoded",
                Body = (string?)j["Body"] ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .ToList();
    }

    private static List<JObject> ParseManyJsonObjects(string content)
    {
        var list = new List<JObject>();
        using var sr = new StringReader(content);
        using var reader = new JsonTextReader(sr) { SupportMultipleContent = true };

        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                continue;
            }

            var obj = JObject.Load(reader);
            list.Add(obj);
        }

        return list;
    }
}

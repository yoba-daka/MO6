using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Models;
using MyProject12.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class CapturedRequestsModelMappingIntegrationTests
{
    private sealed class CapturedRequest
    {
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Replay_CapturedRequests_ShouldMapToTransactionModelCorrectly()
    {
        var captured = LoadCapturedRequests();
        captured.Should().HaveCountGreaterThan(1);

        var reader = new MeshulamWebhookPayloadReader();

        foreach (var req in captured)
        {
            var httpRequest = BuildRequest(req.Method, req.Path, req.ContentType, req.Body);
            var payload = await reader.ReadAsync(httpRequest);

            var transaction = MapTransaction(payload);
            transaction.Should().NotBeNull();
            transaction!.TransactionToken.Should().NotBeNullOrWhiteSpace();
            transaction.PayerEmail.Should().NotBeNullOrWhiteSpace();
            transaction.Sum.Should().NotBeNull();

            if (req.Path.Equals("/meshulam-dd-success", StringComparison.OrdinalIgnoreCase))
            {
                transaction.TransactionId.Should().Be(405369);
                transaction.DirectDebitId.Should().Be(176694);
                transaction.PaymentType.Should().Be(1);
                transaction.Sum.Should().BeApproximately(67f, 0.001f);
                transaction.PayerEmail.Should().Be("boomitgames@gmail.com");
                transaction.StatusCode.Should().Be(2);
            }

            if (req.Path.Equals("/meshulam-response", StringComparison.OrdinalIgnoreCase))
            {
                transaction.TransactionId.Should().Be(407396);
                transaction.DirectDebitId.Should().BeNull();
                transaction.PaymentType.Should().Be(2);
                transaction.Sum.Should().BeApproximately(564f, 0.001f);
                transaction.PayerEmail.Should().Be("boomitgames@gmail.com");
                transaction.StatusCode.Should().Be(2);
            }
        }
    }

    private static Transaction? MapTransaction(MeshulamWebhookPayload payload)
    {
        if (payload.IsJson)
        {
            return MeshulamTransactionMapper.MapJsonToTransaction(payload.RawBody);
        }

        if (payload.HasForm)
        {
            return MeshulamTransactionMapper.MapFormDataToTransaction(payload.FormData);
        }

        return MeshulamTransactionMapper.MapLoosePayloadToTransaction(payload);
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

    private static List<CapturedRequest> LoadCapturedRequests()
    {
        var fullPath = FindRequestsFilePath();
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

    private static string FindRequestsFilePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "requests.txt");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("requests.txt was not found by test file discovery.");
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

            list.Add(JObject.Load(reader));
        }

        return list;
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class PaymentsHarnessRawCaptureFormatTests
{
    private static HttpRequest BuildRequest(string method, string contentType, string body, string queryString = "")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.ContentType = contentType;
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            ctx.Request.QueryString = queryString.StartsWith("?")
                ? new QueryString(queryString)
                : new QueryString("?" + queryString);
        }

        var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        return ctx.Request;
    }

    [Fact]
    public async Task RecordRawWebhook_GetQueryOnly_ShouldBeStoredAsForm_NotUnknown()
    {
        var request = BuildRequest(
            "GET",
            "",
            "",
            "?transactionCode=ABC123&paymentSum=67&payerEmail=query%40example.com");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);
        var store = new PaymentsHarnessStore();

        store.RecordRawWebhook(
            "/meshulam-response",
            payload,
            transaction: null,
            classification: null,
            method: request.Method,
            queryString: request.QueryString.Value,
            headers: string.Empty);

        var snapshot = store.GetSnapshot();
        snapshot.RawWebhooks.Should().HaveCount(1);
        snapshot.RawWebhooks[0].Format.Should().Be("form");
    }

    [Fact]
    public async Task RecordRawWebhook_EmptyProbe_ShouldBeStoredAsEmpty()
    {
        var request = BuildRequest("GET", "", "", "");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);
        var store = new PaymentsHarnessStore();

        store.RecordRawWebhook(
            "/meshulam-response",
            payload,
            transaction: null,
            classification: null,
            method: request.Method,
            queryString: request.QueryString.Value,
            headers: string.Empty);

        var snapshot = store.GetSnapshot();
        snapshot.RawWebhooks.Should().HaveCount(1);
        snapshot.RawWebhooks[0].Format.Should().Be("empty");
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class MeshulamWebhookPayloadReaderTests
{
    private static HttpRequest BuildRequest(string contentType, string body, string queryString = "", string method = "POST")
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
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        return ctx.Request;
    }

    [Fact]
    public async Task ReadAsync_FormUrlEncoded_ShouldParseCapturedRecurringPayload()
    {
        var body = "err=&status=1&data%5Bstatus%5D=%D7%A9%D7%95%D7%9C%D7%9D&data%5BstatusCode%5D=2&data%5BpaymentType%5D=1&data%5Bsum%5D=67&data%5BpayerEmail%5D=boomitgames%40gmail.com&data%5BtransactionId%5D=405369&data%5BtransactionToken%5D=0d6d12462a56ad73d19d068018c37e45&data%5BdirectDebitId%5D=176694&data%5BcustomFields%5D%5BcField1%5D=63bf7286d09a4475a2ee94a888ae0454";
        var request = BuildRequest("application/x-www-form-urlencoded", body);
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.Kind.Should().Be(MeshulamWebhookPayloadKind.Form);
        payload.HasForm.Should().BeTrue();
        payload.GetValue("data[payerEmail]", "payerEmail").Should().Be("boomitgames@gmail.com");
        payload.GetValue("data[transactionToken]", "transactionToken").Should().Be("0d6d12462a56ad73d19d068018c37e45");
        payload.GetValue("data[directDebitId]", "directDebitId").Should().Be("176694");
        payload.GetValue("data[customFields][cField1]", "data.customFields.cField1", "cField1")
            .Should().Be("63bf7286d09a4475a2ee94a888ae0454");
    }

    [Fact]
    public async Task ReadAsync_Json_ShouldParseRecurringWebhookStylePayload()
    {
        var json = """
        {
          "transactionCode": "123456789",
          "paymentType": "הוראת קבע",
          "paymentSum": "67",
          "paymentDate": "20/6/25",
          "payerEmail": "boomitgames@gmail.com",
          "directDebitId": "176694",
          "webhookKey": "abc123"
        }
        """;
        var request = BuildRequest("application/json", json);
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.Kind.Should().Be(MeshulamWebhookPayloadKind.Json);
        payload.IsJson.Should().BeTrue();
        payload.GetValue("paymentType").Should().Be("הוראת קבע");
        payload.GetValue("paymentSum").Should().Be("67");
        payload.GetValue("payerEmail", "email").Should().Be("boomitgames@gmail.com");
        payload.GetValue("directDebitId").Should().Be("176694");
    }

    [Fact]
    public async Task ReadAsync_UrlEncodedBodyWithoutFormContentType_ShouldFallbackToFormMap()
    {
        var body = "status=1&paymentType=%D7%94%D7%95%D7%A8%D7%90%D7%AA+%D7%A7%D7%91%D7%A2&payerEmail=test%40example.com&directDebitId=555";
        var request = BuildRequest("text/plain", body);
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.Kind.Should().Be(MeshulamWebhookPayloadKind.Form);
        payload.GetValue("payerEmail").Should().Be("test@example.com");
        payload.GetValue("directDebitId").Should().Be("555");
        payload.GetValue("paymentType").Should().Be("הוראת קבע");
    }

    [Fact]
    public async Task ReadAsync_ShouldIncludeQueryStringValuesInNormalizedLookup()
    {
        var request = BuildRequest(
            "application/json",
            "{}",
            "?DirectDebit=45088143&Process=84ade426-4569-0c6a-df21-2386ce37e015&transactionToken=abc123");
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.IsJson.Should().BeTrue();
        payload.GetValue("directDebit", "DirectDebit", "directDebitId").Should().Be("45088143");
        payload.GetValue("process", "Process", "processToken").Should().Be("84ade426-4569-0c6a-df21-2386ce37e015");
        payload.GetValue("transactionToken").Should().Be("abc123");
    }

    [Fact]
    public async Task ReadAsync_QueryValues_ShouldNotOverrideBodyValues()
    {
        var request = BuildRequest(
            "application/json",
            "{\"directDebitId\":\"111\",\"processToken\":\"body-token\"}",
            "?directDebitId=999&processToken=query-token");
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.GetValue("directDebitId").Should().Be("111");
        payload.GetValue("processToken").Should().Be("body-token");
    }

    [Fact]
    public async Task ReadAsync_GetQueryOnly_ShouldTreatPayloadAsForm()
    {
        var request = BuildRequest(
            "",
            "",
            "?transactionCode=ABC123&paymentSum=67&payerEmail=query%40example.com&directDebitId=45553893&paymentType=%D7%94%D7%95%D7%A8%D7%90%D7%AA+%D7%A7%D7%91%D7%A2",
            "GET");
        var sut = new MeshulamWebhookPayloadReader();

        var payload = await sut.ReadAsync(request);

        payload.Kind.Should().Be(MeshulamWebhookPayloadKind.Form);
        payload.HasForm.Should().BeTrue();
        payload.GetValue("transactionCode").Should().Be("ABC123");
        payload.GetValue("paymentSum").Should().Be("67");
        payload.GetValue("payerEmail").Should().Be("query@example.com");
        payload.GetValue("directDebitId", "DirectDebit").Should().Be("45553893");
        payload.GetValue("paymentType").Should().Be("הוראת קבע");
    }
}

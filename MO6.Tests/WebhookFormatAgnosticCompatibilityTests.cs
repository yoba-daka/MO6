using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Models;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class WebhookFormatAgnosticCompatibilityTests
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

    private static Transaction? MapUsingControllerEquivalentPath(MeshulamWebhookPayload payload)
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

    [Fact]
    public async Task JsonPayload_ShouldMapInUnifiedPath()
    {
        var json = """
        {
          "status":"שולם",
          "paymentType":"הוראת קבע",
          "paymentSum":"67",
          "paymentDate":"04/3/26",
          "transactionCode":"469268484",
          "payerEmail":"danielpour@gmail.com",
          "directDebitId":45553893
        }
        """;
        var request = BuildRequest("POST", "application/json", json);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MapUsingControllerEquivalentPath(payload);

        tx.Should().NotBeNull();
        tx!.Sum.Should().BeApproximately(67f, 0.001f);
        tx.PaymentType.Should().Be(1);
        tx.DirectDebitId.Should().Be(45553893);
        tx.TransactionToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FormPayload_ShouldMapInUnifiedPath()
    {
        var body = "status=1&data%5Bstatus%5D=%D7%A9%D7%95%D7%9C%D7%9D&data%5BstatusCode%5D=2&data%5BpaymentType%5D=1&data%5Bsum%5D=67&data%5BpayerEmail%5D=boomitgames%40gmail.com&data%5BtransactionId%5D=405369&data%5BtransactionToken%5D=0d6d12462a56ad73d19d068018c37e45&data%5BdirectDebitId%5D=176694";
        var request = BuildRequest("POST", "application/x-www-form-urlencoded", body);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MapUsingControllerEquivalentPath(payload);

        tx.Should().NotBeNull();
        tx!.TransactionId.Should().Be(405369);
        tx.TransactionToken.Should().Be("0d6d12462a56ad73d19d068018c37e45");
        tx.DirectDebitId.Should().Be(176694);
        tx.Sum.Should().BeApproximately(67f, 0.001f);
    }

    [Fact]
    public async Task GetQueryPayload_ShouldMapInUnifiedPath()
    {
        var request = BuildRequest(
            "GET",
            "",
            "",
            "?transactionCode=ABC123&paymentSum=67&paymentType=%D7%94%D7%95%D7%A8%D7%90%D7%AA+%D7%A7%D7%91%D7%A2&payerEmail=query%40example.com&directDebitId=45553893");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MapUsingControllerEquivalentPath(payload);

        tx.Should().NotBeNull();
        tx!.Asmachta.Should().Be("ABC123");
        tx.Sum.Should().BeApproximately(67f, 0.001f);
        tx.PaymentType.Should().Be(1);
        tx.TransactionToken.Should().StartWith("wh:");
    }

    [Fact]
    public async Task FailureStyleQueryPayload_ShouldExposeErrorFieldsAndMapCoreData()
    {
        var request = BuildRequest(
            "GET",
            "",
            "",
            "?error_message=declined&payerEmail=decline%40example.com&transactionCode=DECLINED123&paymentSum=67");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MapUsingControllerEquivalentPath(payload);

        payload.GetValue("error_message", "error", "err").Should().Be("declined");
        tx.Should().NotBeNull();
        tx!.PayerEmail.Should().Be("decline@example.com");
        tx.Asmachta.Should().Be("DECLINED123");
        tx.TransactionToken.Should().StartWith("wh:");
    }
}

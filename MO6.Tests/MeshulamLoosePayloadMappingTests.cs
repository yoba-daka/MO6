using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class MeshulamLoosePayloadMappingTests
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
    public async Task MapLoosePayloadToTransaction_QueryOnly_ShouldMapAndGenerateSyntheticToken()
    {
        var request = BuildRequest(
            "GET",
            "",
            "",
            "?transactionCode=ABC123&paymentSum=67&payerEmail=query%40example.com&directDebitId=45553893&paymentType=%D7%94%D7%95%D7%A8%D7%90%D7%AA+%D7%A7%D7%91%D7%A2");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MeshulamTransactionMapper.MapLoosePayloadToTransaction(payload);

        tx.Should().NotBeNull();
        tx!.Asmachta.Should().Be("ABC123");
        tx.Sum.Should().BeApproximately(67f, 0.001f);
        tx.PayerEmail.Should().Be("query@example.com");
        tx.DirectDebitId.Should().Be(45553893);
        tx.PaymentType.Should().Be(1);
        tx.TransactionToken.Should().StartWith("wh:");
    }

    [Fact]
    public async Task MapLoosePayloadToTransaction_EmptyProbe_ShouldReturnNull()
    {
        var request = BuildRequest("GET", "", "", "");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var tx = MeshulamTransactionMapper.MapLoosePayloadToTransaction(payload);

        tx.Should().BeNull();
    }
}

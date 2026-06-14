using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class MeshulamWebhookPayloadNormalizationIntegrationTests
{
    [Fact]
    public async Task EndToEnd_FormPayload_ShouldNormalizeBracketAndDotKeysEqually()
    {
        var ctx = new DefaultHttpContext();
        var body = "data%5BcustomFields%5D%5BcField1%5D=tok123&data%5BpayerEmail%5D=u%40e.com&data%5BdirectDebitId%5D=176694";
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;

        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(ctx.Request);

        payload.GetValue("data[customFields][cField1]").Should().Be("tok123");
        payload.GetValue("data.customFields.cField1").Should().Be("tok123");
        payload.GetValue("customFields.cField1").Should().BeNull();
        payload.GetValue("data[payerEmail]", "payerEmail").Should().Be("u@e.com");
        payload.GetValue("data[directDebitId]", "directDebitId").Should().Be("176694");
    }

    [Fact]
    public async Task EndToEnd_JsonPayload_ShouldExposeNestedPathsForControllerTokenLookup()
    {
        var ctx = new DefaultHttpContext();
        var json = """
        {
          "data": {
            "customFields": {
              "cField1": "tok-json"
            },
            "payerEmail": "json@example.com"
          },
          "directDebitId": "9001"
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;

        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(ctx.Request);

        payload.GetValue("data.customFields.cField1", "cField1").Should().Be("tok-json");
        payload.GetValue("data[payerEmail]", "payerEmail", "data.payerEmail").Should().Be("json@example.com");
        payload.GetValue("directDebitId").Should().Be("9001");
    }
}

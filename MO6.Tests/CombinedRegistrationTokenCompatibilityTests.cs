using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class CombinedRegistrationTokenCompatibilityTests
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

    private static string? ExtractRegistrationTokenEquivalent(MeshulamWebhookPayload payload)
    {
        return payload.GetValue(
            "data[customFields][cField1]",
            "data[cField1]",
            "customFields[cField1]",
            "customFields.cField1",
            "data.customFields.cField1",
            "cField1");
    }

    [Fact]
    public async Task TokenExtraction_FormDataCustomFieldsBracketShape_ShouldWork()
    {
        var body = "data%5BcustomFields%5D%5BcField1%5D=tok-form-1&data%5BpayerEmail%5D=u%40e.com";
        var request = BuildRequest("POST", "application/x-www-form-urlencoded", body);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var token = ExtractRegistrationTokenEquivalent(payload);

        token.Should().Be("tok-form-1");
    }

    [Fact]
    public async Task TokenExtraction_JsonNestedCustomFields_ShouldWork()
    {
        var json = """
        {
          "data": {
            "customFields": {
              "cField1": "tok-json-nested"
            },
            "payerEmail": "u@e.com"
          }
        }
        """;
        var request = BuildRequest("POST", "application/json", json);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var token = ExtractRegistrationTokenEquivalent(payload);

        token.Should().Be("tok-json-nested");
    }

    [Fact]
    public async Task TokenExtraction_JsonRootCField1_ShouldWork()
    {
        var json = """
        {
          "cField1": "tok-json-root",
          "payerEmail": "u@e.com"
        }
        """;
        var request = BuildRequest("POST", "application/json", json);
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var token = ExtractRegistrationTokenEquivalent(payload);

        token.Should().Be("tok-json-root");
    }

    [Fact]
    public async Task TokenExtraction_GetQueryCField1_ShouldWork()
    {
        var request = BuildRequest("GET", "", "", "?cField1=tok-query-1&payerEmail=u%40e.com");
        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(request);

        var token = ExtractRegistrationTokenEquivalent(payload);

        token.Should().Be("tok-query-1");
        payload.HasForm.Should().BeTrue();
    }
}

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyProject12.Services;
using System.Net.Http;
using Xunit;

namespace MO6.Tests;

public class PaymentsHarnessCreatePaymentProcessLiveTests
{
    [Fact]
    public async Task CreatePaymentProcess_Yearly_ReturnsCheckoutUrl()
    {
        if (!LiveTestsEnabled())
        {
            return;
        }

        var sut = new PaymentsHarnessSandboxClient(new TestHttpClientFactory(), BuildSandboxConfig());
        var baseUrl = GetBaseUrl();
        var amount = GetFloat("MO6_TEST_YEARLY_SUM", 348f);
        var cField1 = $"HARNESS_{Guid.NewGuid():N}_Yearly";
        var harnessToken = GetHarnessToken();

        var result = await sut.CreatePaymentProcessAsync(false, amount, baseUrl, cField1, harnessToken);

        Console.WriteLine($"YEARLY_URL: {result.CheckoutUrl}");
        Console.WriteLine($"YEARLY_REQUEST: {result.RawRequest}");
        Console.WriteLine($"YEARLY_RAW: {result.RawResponse}");

        result.Success.Should().BeTrue($"expected sandbox createPaymentProcess success. error={result.Error}, raw={result.RawResponse}");
        result.CheckoutUrl.Should().NotBeNullOrWhiteSpace();
        result.CheckoutUrl.Should().MatchRegex("^https?://");
    }

    [Fact]
    public async Task CreatePaymentProcess_Monthly_ReturnsCheckoutUrl()
    {
        if (!LiveTestsEnabled())
        {
            return;
        }

        var sut = new PaymentsHarnessSandboxClient(new TestHttpClientFactory(), BuildSandboxConfig());
        var baseUrl = GetBaseUrl();
        var amount = GetFloat("MO6_TEST_MONTHLY_SUM", 35f);
        var cField1 = $"HARNESS_{Guid.NewGuid():N}_Monthly";
        var harnessToken = GetHarnessToken();

        var result = await sut.CreatePaymentProcessAsync(true, amount, baseUrl, cField1, harnessToken);

        Console.WriteLine($"MONTHLY_URL: {result.CheckoutUrl}");
        Console.WriteLine($"MONTHLY_REQUEST: {result.RawRequest}");
        Console.WriteLine($"MONTHLY_RAW: {result.RawResponse}");

        result.Success.Should().BeTrue($"expected sandbox createPaymentProcess success. error={result.Error}, raw={result.RawResponse}");
        result.CheckoutUrl.Should().NotBeNullOrWhiteSpace();
        result.CheckoutUrl.Should().MatchRegex("^https?://");
    }

    private static bool LiveTestsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("MO6_RUN_LIVE_PAYMENT_TESTS"),
            "1",
            StringComparison.Ordinal);
    }

    private static string GetBaseUrl()
    {
        return Environment.GetEnvironmentVariable("MO6_TEST_BASE_URL")?.TrimEnd('/')
               ?? "https://mo6.co.il";
    }

    private static string GetHarnessToken()
    {
        return Environment.GetEnvironmentVariable("MO6_TEST_HARNESS_TOKEN")
               ?? "3c549a772e2751ac5dd6520d96fb2c05010b99fb48154a82e9bdf6adaf2c5cc2";
    }

    private static float GetFloat(string envVar, float fallback)
    {
        return float.TryParse(Environment.GetEnvironmentVariable(envVar), out var value)
            ? value
            : fallback;
    }

    private static IConfiguration BuildSandboxConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Meshulam:SandboxUserID"] = "530c0ed0c411ce71",
                ["Meshulam:SandboxYearlyPageCode"] = "a54f4954c06f",
                ["Meshulam:SandboxMonthlyPageCode"] = "0614c4d8b7a0"
            })
            .Build();
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

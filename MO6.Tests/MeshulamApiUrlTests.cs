using FluentAssertions;
using MyProject12.Services;
using Xunit;

namespace MO6.Tests;

public class MeshulamApiUrlTests
{
    [Fact]
    public void NormalizeBaseAddress_AppendsTrailingSlash_WhenMissing()
    {
        var normalized = MeshulamApiUrl.NormalizeBaseAddress("https://secure.meshulam.co.il/api/light/server/1.0");

        normalized.Should().Be("https://secure.meshulam.co.il/api/light/server/1.0/");
        var resolved = new Uri(new Uri(normalized), "updateDirectDebit/").ToString();
        resolved.Should().Be("https://secure.meshulam.co.il/api/light/server/1.0/updateDirectDebit/");
    }

    [Fact]
    public void NormalizeBaseAddress_KeepsTrailingSlash_WhenPresent()
    {
        var normalized = MeshulamApiUrl.NormalizeBaseAddress("https://sandbox.meshulam.co.il/api/light/server/1.0/");

        normalized.Should().Be("https://sandbox.meshulam.co.il/api/light/server/1.0/");
    }

    [Fact]
    public void NormalizeBaseAddress_Throws_WhenMissing()
    {
        var act = () => MeshulamApiUrl.NormalizeBaseAddress("  ");

        act.Should().Throw<InvalidOperationException>();
    }
}

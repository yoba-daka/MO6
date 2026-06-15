using FluentAssertions;
using MyProject12.Services;
using Xunit;

namespace MO6.Tests;

public class MeshulamDirectDebitResponseParserTests
{
    [Fact]
    public void ParseUpdateDirectDebit_EmptyStringData_TreatsProviderSuccessAsStrictSuccess()
    {
        var result = MeshulamDirectDebitResponseParser.ParseUpdateDirectDebit(
            "{\"status\":1,\"err\":\"\",\"data\":\"\"}",
            disableDirectDebit: true);

        result.ProviderSuccess.Should().BeTrue();
        result.IsExpectedChangeStatus.Should().BeTrue();
        result.IsStrictSuccess.Should().BeTrue();
        result.ChangeStatus.Should().BeEmpty();
    }

    [Fact]
    public void ParseUpdateDirectDebit_ObjectChangeStatusTwo_TreatsCancelAsStrictSuccess()
    {
        var result = MeshulamDirectDebitResponseParser.ParseUpdateDirectDebit(
            "{\"status\":1,\"err\":\"\",\"data\":{\"changeStatus\":\"2\"}}",
            disableDirectDebit: true);

        result.IsStrictSuccess.Should().BeTrue();
        result.ChangeStatus.Should().Be("2");
    }

    [Fact]
    public void ParseUpdateDirectDebit_EnableWithCancelChangeStatus_IsProviderSuccessButUnexpectedStatus()
    {
        var result = MeshulamDirectDebitResponseParser.ParseUpdateDirectDebit(
            "{\"status\":1,\"err\":\"\",\"data\":{\"changeStatus\":\"2\"}}",
            disableDirectDebit: false);

        result.ProviderSuccess.Should().BeTrue();
        result.IsExpectedChangeStatus.Should().BeFalse();
        result.IsStrictSuccess.Should().BeFalse();
    }
}

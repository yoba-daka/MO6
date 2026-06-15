using MyProject12.Services;
using Xunit;

public class MeshulamCreatePaymentResponseParserTests
{
    [Fact]
    public void Parse_SuccessObjectData_ReturnsCheckoutUrl()
    {
        var result = MeshulamCreatePaymentResponseParser.Parse(
            "{\"status\":1,\"err\":\"\",\"data\":{\"url\":\"https://sandbox.meshulam.co.il/checkout\"}}");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Status);
        Assert.Equal("https://sandbox.meshulam.co.il/checkout", result.Url);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Parse_FailedEmptyStringData_DoesNotThrow_AndReturnsProviderError()
    {
        var result = MeshulamCreatePaymentResponseParser.Parse(
            "{\"status\":0,\"err\":{\"id\":716,\"message\":\"invalid payment process\"},\"data\":\"\"}");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Status);
        Assert.Equal(string.Empty, result.Url);
        Assert.Equal("invalid payment process", result.Error);
    }

    [Fact]
    public void Parse_SuccessEmptyStringData_DoesNotThrow_AndMarksFailure()
    {
        var result = MeshulamCreatePaymentResponseParser.Parse(
            "{\"status\":1,\"err\":\"\",\"data\":\"\"}");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.Status);
        Assert.Equal(string.Empty, result.Url);
        Assert.Equal("createPaymentProcess returned success without a checkout URL.", result.Error);
    }
}

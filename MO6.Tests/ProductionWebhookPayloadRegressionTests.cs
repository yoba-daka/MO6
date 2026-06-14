using FluentAssertions;
using MyProject12.Services;
using Xunit;

namespace MO6.Tests;

public class ProductionWebhookPayloadRegressionTests
{
    [Fact]
    public void JsonWebhook_RegularHebrewType_MapsAsRegular_WithSyntheticToken()
    {
        const string payload = """
        {
          "webhookKey":"84ade426-4569-0c6a-df21-2386ce37e015",
          "transactionCode":"f2lKGFlcike2M0jvkXI6TA==",
          "paymentSum":"564",
          "paymentType":"רגיל",
          "paymentDate":"20/2/26",
          "asmachta":"466751796",
          "payerEmail":"elianebth@gmail.com"
        }
        """;

        var tx = MeshulamTransactionMapper.MapJsonToTransaction(payload);

        tx.Should().NotBeNull();
        tx.TransactionToken.Should().StartWith("wh:");
        tx.TransactionId.Should().BeNull();
        tx.PaymentType.Should().Be(2);
        tx.DirectDebitId.Should().BeNull();
        tx.StatusCode.Should().Be(2);
        tx.CardToken.Should().NotBeNull();
        tx.CardExp.Should().NotBeNull();
        tx.CardBrand.Should().NotBeNull();
        tx.CardType.Should().NotBeNull();
        tx.CardSuffix.Should().NotBeNull();
        tx.ProcessToken.Should().NotBeNullOrWhiteSpace();
        tx.Description.Should().NotBeNull();
        tx.FullName.Should().NotBeNull();
        tx.PayerPhone.Should().NotBeNull();
        tx.PayerEmail.Should().NotBeNull();
        tx.PaymentDate.Should().NotBeNull();
        tx.Asmachta.Should().NotBeNull();
    }

    [Fact]
    public void JsonWebhook_WithMissingOptionalFields_ShouldStillProduceNonNullRequiredStrings()
    {
        const string payload = """
        {
          "webhookKey":"84ade426-4569-0c6a-df21-2386ce37e015",
          "transactionCode":"f2lKGFlcike2M0jvkXI6TA==",
          "paymentSum":"564",
          "paymentType":"רגיל",
          "paymentDate":"20/2/26",
          "asmachta":"466751796"
        }
        """;

        var tx = MeshulamTransactionMapper.MapJsonToTransaction(payload);

        tx.Status.Should().NotBeNullOrWhiteSpace();
        tx.TransactionToken.Should().NotBeNullOrWhiteSpace();
        tx.PaymentDate.Should().NotBeNull();
        tx.Asmachta.Should().NotBeNull();
        tx.Description.Should().NotBeNull();
        tx.FullName.Should().NotBeNull();
        tx.PayerPhone.Should().NotBeNull();
        tx.PayerEmail.Should().NotBeNull();
        tx.CardSuffix.Should().NotBeNull();
        tx.CardType.Should().NotBeNull();
        tx.CardBrand.Should().NotBeNull();
        tx.CardExp.Should().NotBeNull();
        tx.ProcessToken.Should().NotBeNull();
        tx.CardToken.Should().NotBeNull();
    }

    [Fact]
    public void JsonWebhook_RecurringHebrewType_MapsAsMonthly_WithDirectDebit()
    {
        const string payload = """
        {
          "webhookKey":"84ade426-4569-0c6a-df21-2386ce37e015",
          "transactionCode":"cimItersouPpLWxggxznYg==",
          "paymentSum":"67",
          "paymentType":"הוראת קבע",
          "paymentDate":"20/2/26",
          "asmachta":"466720174",
          "payerEmail":"afk1281@gmail.com",
          "directDebitId":45088143
        }
        """;

        var tx = MeshulamTransactionMapper.MapJsonToTransaction(payload);

        tx.Should().NotBeNull();
        tx.TransactionToken.Should().StartWith("wh:");
        tx.TransactionId.Should().BeNull();
        tx.PaymentType.Should().Be(1);
        tx.DirectDebitId.Should().Be(45088143);
        tx.StatusCode.Should().Be(2);
    }

    [Fact]
    public void JsonWebhook_WithoutPayerEmail_StillBuildsStableSyntheticToken()
    {
        const string payload = """
        {
          "webhookKey":"84ade426-4569-0c6a-df21-2386ce37e015",
          "transactionCode":"6GTqaqciQ4Q3k/RzUC3PeQ==",
          "paymentSum":"2850",
          "paymentType":"רגיל",
          "paymentDate":"20/2/26",
          "asmachta":"466741002",
          "payerEmail":""
        }
        """;

        var tx1 = MeshulamTransactionMapper.MapJsonToTransaction(payload);
        var tx2 = MeshulamTransactionMapper.MapJsonToTransaction(payload);

        tx1.TransactionToken.Should().StartWith("wh:");
        tx1.TransactionToken.Should().Be(tx2.TransactionToken);
    }
}

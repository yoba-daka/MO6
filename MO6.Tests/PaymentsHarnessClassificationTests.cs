using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using MyProject12.Models;
using MyProject12.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MO6.Tests;

public class PaymentsHarnessClassificationTests
{
    [Fact]
    public void Classify_WhenCFieldMatchesRun_ShouldMarkAsHarness()
    {
        var store = new PaymentsHarnessStore();
        var run = store.CreateRun(HarnessRunType.Monthly);
        var cField = PaymentsHarnessStore.BuildHarnessCField(run.RunId, run.Type);
        store.MarkRunCreated(run, "https://sandbox.example/checkout", cField);

        var payload = CreateFormPayload(new Dictionary<string, string>
        {
            ["data[cField1]"] = cField
        });
        var transaction = CreateTransaction("tx-cfield-1", "proc-cfield-1", null);

        var classification = store.Classify(payload, transaction);

        classification.IsHarness.Should().BeTrue();
        classification.RunId.Should().Be(run.RunId);
        classification.Reason.Should().Be("cField1");
    }

    [Fact]
    public void Classify_WhenDirectDebitMappedToRun_ShouldMarkAsHarness()
    {
        var store = new PaymentsHarnessStore();
        var run = store.CreateRun(HarnessRunType.Monthly);
        var cField = PaymentsHarnessStore.BuildHarnessCField(run.RunId, run.Type);
        store.MarkRunCreated(run, "https://sandbox.example/checkout", cField);

        var correlated = CreateTransaction("tx-map-1", "proc-map-1", 45088143);
        store.UpdateRunFromWebhook(run.RunId, correlated);

        var payload = CreateFormPayload(new Dictionary<string, string>());
        var recurring = CreateTransaction("tx-map-2", "proc-map-2", 45088143);

        var classification = store.Classify(payload, recurring);

        classification.IsHarness.Should().BeTrue();
        classification.RunId.Should().Be(run.RunId);
        classification.Reason.Should().Be("directDebitId");
    }

    [Fact]
    public void Classify_WhenNoCorrelationExists_ShouldStayProduction()
    {
        var store = new PaymentsHarnessStore();

        var payload = CreateFormPayload(new Dictionary<string, string>
        {
            ["data[payerEmail]"] = "user@example.com"
        });
        var transaction = CreateTransaction("tx-prod-1", "proc-prod-1", 99900123);

        var classification = store.Classify(payload, transaction);

        classification.IsHarness.Should().BeFalse();
        classification.RunId.Should().BeNullOrEmpty();
        classification.Reason.Should().Be("production");
    }

    private static MeshulamWebhookPayload CreateFormPayload(Dictionary<string, string> values)
    {
        var form = new FormCollection(
            values.ToDictionary(
                x => x.Key,
                x => new StringValues(x.Value),
                StringComparer.OrdinalIgnoreCase));

        return new MeshulamWebhookPayload(
            MeshulamWebhookPayloadKind.Form,
            rawBody: string.Empty,
            contentType: "application/x-www-form-urlencoded",
            formData: form,
            jsonData: new JObject());
    }

    private static Transaction CreateTransaction(string transactionToken, string processToken, int? directDebitId)
    {
        return new Transaction
        {
            Created = DateTime.UtcNow,
            Status = "שולם",
            StatusCode = 2,
            TransactionToken = transactionToken,
            TransactionTypeId = 1,
            PaymentType = 1,
            Sum = 67f,
            PaymentDate = "20/2/26",
            Asmachta = "A-123",
            Description = "Harness classification test",
            FullName = "Test User",
            PayerPhone = "0500000000",
            PayerEmail = "test@example.com",
            CardSuffix = "1234",
            CardType = "Local",
            CardBrand = "Visa",
            CardExp = "12/30",
            ProcessToken = processToken,
            CardToken = "card-token",
            DirectDebitId = directDebitId
        };
    }
}

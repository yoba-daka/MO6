using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Services;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class Recurring2024CompatibilityIntegrationTests
{
    [Fact]
    public async Task JsonRecurringPayload_In2024Shape_ShouldStillSatisfyCurrentRecurringFlow()
    {
        // 2024-style recurring webhook payload shape (root-level keys, no data[...] wrapper).
        var json = """
        {
          "status": "שולם",
          "paymentType": "הוראת קבע",
          "paymentSum": "67",
          "paymentsNum": "0",
          "allPaymentNum": "1",
          "firstPaymentSum": "0",
          "periodicalPaymentSum": "0",
          "paymentDate": "20/6/25",
          "paymentDesc": "",
          "fullName": "דוד שלמה",
          "payerPhone": "0543040260",
          "payerEmail": "boomitgames@gmail.com",
          "directDebitId": "176694",
          "transactionCode": "118146394",
          "webhookKey": "d94dc51461bd33f39ada1abf9b9536d4"
        }
        """;

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/meshulam-dd-success";
        ctx.Request.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;

        var reader = new MeshulamWebhookPayloadReader();
        var payload = await reader.ReadAsync(ctx.Request);

        var transaction = MeshulamTransactionMapper.MapJsonToTransaction(payload.RawBody);

        // Mapping correctness
        transaction.Sum.Should().BeApproximately(67f, 0.001f);
        transaction.PaymentType.Should().Be(1); // "הוראת קבע" -> direct debit
        transaction.DirectDebitId.Should().Be(176694);
        transaction.PayerEmail.Should().Be("boomitgames@gmail.com");
        transaction.PaymentDate.Should().Be("20/6/25");
        transaction.TransactionToken.Should().StartWith("wh:"); // synthetic token when provider token missing

        // Current recurring flow acceptance conditions
        var isSuccessful = transaction.StatusCode == 2 ||
                           string.Equals(transaction.Status, "שולם", StringComparison.OrdinalIgnoreCase);
        isSuccessful.Should().BeTrue();
        transaction.TransactionToken.Should().NotBeNullOrWhiteSpace();
        transaction.Sum.Should().NotBeNull();

        // Equivalent to current monthly classification intent:
        // paymentType==1 OR directDebitId exists => monthly recurring.
        var treatedAsMonthly = (transaction.PaymentType ?? 0) == 1 || transaction.DirectDebitId.HasValue;
        treatedAsMonthly.Should().BeTrue();
    }
}

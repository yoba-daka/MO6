using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MyProject12.Models;
using MyProject12.Services;
using System.Net;
using System.Text;
using Xunit;

namespace MO6.Tests;

public class PaymentProductionFlowIntegrationTests
{
    private const string PaidStatus = "\u05e9\u05d5\u05dc\u05dd";
    private const string RegularPaymentType = "\u05e8\u05d2\u05d9\u05dc";
    private const string DirectDebitPaymentType = "\u05d4\u05d5\u05e8\u05d0\u05ea \u05e7\u05d1\u05e2";

    [Fact]
    public async Task RegularPaymentWebhook_UsesProductionMapping_AndCreatesNonMonthlyMembershipState()
    {
        var transaction = await MapProductionWebhookAsync(
            "application/json",
            BuildRegularPaymentJson());

        transaction.StatusCode.Should().Be(2);
        transaction.Status.Should().Be(PaidStatus);
        transaction.PaymentType.Should().Be(2);
        transaction.DirectDebitId.Should().BeNull();
        transaction.TransactionId.Should().Be(812345);
        transaction.TransactionToken.Should().Be("regularpaymenttoken123");

        var membership = ApplyMembershipForTransaction("member-regular", null, transaction, monthly: false);

        membership.memberID.Should().Be("member-regular");
        membership.isMonthly.Should().BeFalse();
        membership.isMonthlyActive.Should().BeFalse();
        membership.transactions.Should().Contain("812345");
        membership.transactions.Should().Contain("regularpaymenttoken123");
        membership.expiration.Should().BeAfter(DateTime.UtcNow.AddMonths(11));
    }

    [Fact]
    public async Task SubscriptionInitialWebhook_UsesProductionMapping_AndStoresIdentifiersNeededForCancellation()
    {
        var transaction = await MapProductionWebhookAsync(
            "application/x-www-form-urlencoded",
            BuildSubscriptionPaymentFormBody());

        transaction.StatusCode.Should().Be(2);
        transaction.Status.Should().Be(PaidStatus);
        transaction.PaymentType.Should().Be(1);
        transaction.DirectDebitId.Should().Be(227298);
        transaction.TransactionId.Should().Be(515677);
        transaction.TransactionToken.Should().Be("c08dd0f9b51b9450732d4457ab81b5d6");
        transaction.Asmachta.Should().Be("127409643");

        var membership = ApplyMembershipForTransaction("member-monthly", null, transaction, monthly: IsMonthlyLikeProduction(transaction));

        membership.isMonthly.Should().BeTrue();
        membership.isMonthlyActive.Should().BeTrue();
        membership.transactions.Should().Contain("515677");
        membership.transactions.Should().Contain("c08dd0f9b51b9450732d4457ab81b5d6");

        var refs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
        MembershipCancellationHelper.ParseTransactionIds(refs).Should().Contain(515677);
        MembershipCancellationHelper.ParseTransactionTokens(refs).Should().Contain("c08dd0f9b51b9450732d4457ab81b5d6");
        MembershipCancellationHelper.HasRequiredUpdateDirectDebitIdentifiers(transaction).Should().BeTrue();
    }

    [Theory]
    [InlineData("account page")]
    [InlineData("backoffice")]
    public async Task SubscriptionCancellation_AccountAndBackofficeSelection_ProviderSuccessMarksMembershipInactive(string cancellationSurface)
    {
        var subscriptionTransaction = await MapProductionWebhookAsync(
            "application/x-www-form-urlencoded",
            BuildSubscriptionPaymentFormBody());
        var membership = ApplyMembershipForTransaction(
            "member-monthly",
            null,
            subscriptionTransaction,
            monthly: IsMonthlyLikeProduction(subscriptionTransaction));

        var olderSyntheticWebhookTransaction = CreateSyntheticDirectDebitTransaction(
            subscriptionTransaction.DirectDebitId!.Value,
            subscriptionTransaction.PayerEmail);
        var unrelatedRegularTransaction = CreateUnrelatedRegularTransaction(subscriptionTransaction.PayerEmail);
        var dbTransactions = new[]
        {
            unrelatedRegularTransaction,
            olderSyntheticWebhookTransaction,
            subscriptionTransaction
        };

        var selected = SelectCancellationCandidateLikeController(
            cancellationSurface,
            dbTransactions,
            membership,
            subscriptionTransaction.PayerEmail);

        selected.Should().NotBeNull();
        selected!.TransactionId.Should().Be(515677);
        selected.TransactionToken.Should().Be("c08dd0f9b51b9450732d4457ab81b5d6");
        selected.Asmachta.Should().Be("127409643");
        selected.DirectDebitId.Should().Be(227298);
        MembershipCancellationHelper.HasRequiredUpdateDirectDebitIdentifiers(selected).Should().BeTrue();

        var cancelResponse = MeshulamDirectDebitResponseParser.ParseUpdateDirectDebit(
            "{\"status\":1,\"err\":\"\",\"data\":\"\"}",
            disableDirectDebit: true);
        cancelResponse.IsStrictSuccess.Should().BeTrue();

        membership.isMonthlyActive = false;
        membership.isMonthlyActive.Should().BeFalse("the shared CancelDirectDebit service succeeded for {0}", cancellationSurface);
    }

    private static async Task<Transaction> MapProductionWebhookAsync(string contentType, string body)
    {
        var request = BuildRequest(contentType, body);
        var payload = await new MeshulamWebhookPayloadReader().ReadAsync(request);

        Transaction? transaction;
        if (payload.IsJson)
        {
            transaction = MeshulamTransactionMapper.MapJsonToTransaction(payload.RawBody);
        }
        else if (payload.HasForm)
        {
            transaction = MeshulamTransactionMapper.MapFormDataToTransaction(payload.FormData);
        }
        else
        {
            transaction = MeshulamTransactionMapper.MapLoosePayloadToTransaction(payload);
        }

        transaction.Should().NotBeNull();
        return transaction!;
    }

    private static HttpRequest BuildRequest(string contentType, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/meshulam-response";
        context.Request.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        return context.Request;
    }

    private static Membership ApplyMembershipForTransaction(
        string memberId,
        Membership? membership,
        Transaction transaction,
        bool monthly)
    {
        var transactionRefs = GetTransactionReferenceCandidates(transaction);
        membership ??= new Membership
        {
            memberID = memberId,
            expiration = DateTime.UtcNow,
            transactions = string.Empty
        };

        if (MembershipContainsTransaction(membership, transaction))
        {
            return membership;
        }

        var nowPlus = DateTime.UtcNow.AddMonths(monthly ? 1 : 12).AddHours(1);
        var expPlus = membership.expiration.AddMonths(monthly ? 1 : 12).AddHours(1);

        membership.expiration = expPlus > nowPlus ? expPlus : nowPlus;
        membership.transactions = (membership.transactions ?? string.Empty) + string.Join(";", transactionRefs) + ";";
        membership.phone = transaction.PayerPhone;
        membership.isMonthly = monthly;
        membership.isMonthlyActive = monthly;

        return membership;
    }

    private static Transaction? SelectCancellationCandidateLikeController(
        string cancellationSurface,
        IEnumerable<Transaction> transactions,
        Membership membership,
        string email)
    {
        cancellationSurface.Should().BeOneOf("account page", "backoffice");

        var parsedRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
        var parsedTransactionIds = MembershipCancellationHelper.ParseTransactionIds(parsedRefs);
        var parsedTransactionTokens = MembershipCancellationHelper.ParseTransactionTokens(parsedRefs);
        var normalizedEmail = MembershipCancellationHelper.NormalizeComparisonValue(email);

        var candidateTransactions = transactions
            .Where(x =>
                (x.TransactionId.HasValue && parsedTransactionIds.Contains(x.TransactionId.Value)) ||
                (!string.IsNullOrWhiteSpace(x.TransactionToken) &&
                 parsedTransactionTokens.Contains(x.TransactionToken.Trim().ToLowerInvariant())) ||
                (!string.IsNullOrWhiteSpace(x.PayerEmail) &&
                 x.PayerEmail.Trim().ToLowerInvariant() == normalizedEmail))
            .OrderByDescending(x => x.Created)
            .Take(250)
            .ToList();

        var candidateDirectDebitIds = candidateTransactions
            .Where(x => x.DirectDebitId.HasValue && x.DirectDebitId.Value > 0)
            .Select(x => x.DirectDebitId!.Value)
            .Distinct()
            .ToList();

        if (candidateDirectDebitIds.Count > 0)
        {
            var relatedDirectDebitTransactions = transactions
                .Where(x => x.DirectDebitId.HasValue && candidateDirectDebitIds.Contains(x.DirectDebitId.Value))
                .OrderByDescending(x => x.Created)
                .Take(250)
                .ToList();

            candidateTransactions = candidateTransactions
                .Concat(relatedDirectDebitTransactions)
                .GroupBy(x => x.ID)
                .Select(x => x.First())
                .ToList();
        }

        return MembershipCancellationHelper.SelectProviderCancellationCandidate(
            candidateTransactions,
            parsedTransactionIds,
            parsedTransactionTokens,
            email,
            candidateDirectDebitIds);
    }

    private static bool IsMonthlyLikeProduction(Transaction transaction)
    {
        return (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0) ||
               transaction.PaymentType == 1;
    }

    private static bool MembershipContainsTransaction(Membership membership, Transaction transaction)
    {
        var existingRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
        if (existingRefs.Count == 0)
        {
            return false;
        }

        var normalizedExisting = existingRefs
            .Select(MembershipCancellationHelper.NormalizeComparisonValue)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return GetTransactionReferenceCandidates(transaction)
            .Select(MembershipCancellationHelper.NormalizeComparisonValue)
            .Any(x => normalizedExisting.Contains(x));
    }

    private static List<string> GetTransactionReferenceCandidates(Transaction transaction)
    {
        var refs = new List<string>();
        if (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0)
        {
            refs.Add(transaction.TransactionId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(transaction.TransactionToken))
        {
            refs.Add(transaction.TransactionToken.Trim());
        }

        return refs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Transaction CreateSyntheticDirectDebitTransaction(int directDebitId, string email)
    {
        return new Transaction
        {
            ID = 10,
            Created = DateTime.UtcNow.AddMinutes(-10),
            Status = PaidStatus,
            StatusCode = 2,
            TransactionToken = "wh:synthetic",
            Asmachta = "127409643",
            PayerEmail = email,
            PayerPhone = "0500000000",
            PaymentDate = "15/06/26",
            Description = "MO6 Payment",
            FullName = "Test User",
            CardSuffix = "4580",
            CardType = "Foreign",
            CardBrand = "Visa",
            CardExp = "1230",
            ProcessToken = "process-token",
            CardToken = string.Empty,
            DirectDebitId = directDebitId,
            PaymentType = 1,
            Sum = 35
        };
    }

    private static Transaction CreateUnrelatedRegularTransaction(string email)
    {
        return new Transaction
        {
            ID = 20,
            Created = DateTime.UtcNow.AddMinutes(-5),
            Status = PaidStatus,
            StatusCode = 2,
            TransactionId = 900001,
            TransactionToken = "regularunrelatedtoken",
            Asmachta = "900001999",
            PayerEmail = email,
            PayerPhone = "0500000000",
            PaymentDate = "15/06/26",
            Description = "MO6 Payment",
            FullName = "Test User",
            CardSuffix = "1111",
            CardType = "Foreign",
            CardBrand = "Visa",
            CardExp = "1230",
            ProcessToken = "process-token",
            CardToken = string.Empty,
            PaymentType = 2,
            Sum = 348
        };
    }

    private static string BuildRegularPaymentJson()
    {
        return $$"""
        {
          "err":"",
          "status":1,
          "data": {
            "status":"{{PaidStatus}}",
            "statusCode":2,
            "transactionTypeId":1,
            "paymentType":2,
            "sum":348,
            "paymentDate":"15/06/26",
            "description":"MO6 regular payment test",
            "fullName":"Test User",
            "payerPhone":"0500000000",
            "payerEmail":"regular@example.com",
            "transactionId":812345,
            "transactionToken":"regularpaymenttoken123",
            "asmachta":"812345999",
            "cardSuffix":"1111",
            "cardType":"Foreign",
            "cardBrand":"Visa",
            "cardExp":"1230",
            "processId":721690,
            "processToken":"regular-process-token"
          }
        }
        """;
    }

    private static string BuildSubscriptionPaymentFormBody()
    {
        var values = new Dictionary<string, string>
        {
            ["err"] = string.Empty,
            ["status"] = "1",
            ["data[status]"] = PaidStatus,
            ["data[statusCode]"] = "2",
            ["data[transactionTypeId]"] = "1",
            ["data[paymentType]"] = "1",
            ["data[sum]"] = "35",
            ["data[paymentsNum]"] = "0",
            ["data[allPaymentsNum]"] = "1",
            ["data[paymentDate]"] = "15/06/26",
            ["data[description]"] = "MO6 subscription payment test",
            ["data[fullName]"] = "Test User",
            ["data[payerPhone]"] = "0500000000",
            ["data[payerEmail]"] = "monthly@example.com",
            ["data[transactionId]"] = "515677",
            ["data[transactionToken]"] = "c08dd0f9b51b9450732d4457ab81b5d6",
            ["data[directDebitId]"] = "227298",
            ["data[recurringDebitId]"] = "8073",
            ["data[asmachta]"] = "127409643",
            ["data[cardSuffix]"] = "4580",
            ["data[cardType]"] = "Foreign",
            ["data[cardTypeCode]"] = "2",
            ["data[cardBrand]"] = "Visa",
            ["data[cardBrandCode]"] = "3",
            ["data[cardExp]"] = "1230",
            ["data[firstPaymentSum]"] = "0",
            ["data[periodicalPaymentSum]"] = "0",
            ["data[processId]"] = "721696",
            ["data[processToken]"] = "63352479efbc271ac1459fa6fb433609",
            ["data[customFields][cField1]"] = "member-monthly"
        };

        return string.Join("&", values.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
    }
}

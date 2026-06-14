using FluentAssertions;
using MyProject12.Models;
using MyProject12.Services;
using Xunit;

namespace MO6.Tests;

public class MembershipCancellationHelperTests
{
    [Fact]
    public void ParseTransactionReferences_ShouldNormalizeAndIgnoreGiftMarkers()
    {
        var raw = " 123 ; wh:abc ; gift-month ; \"456\" ; חודש-מתנה ; token-1 ";

        var refs = MembershipCancellationHelper.ParseTransactionReferences(raw);

        refs.Should().Contain("123");
        refs.Should().Contain("wh:abc");
        refs.Should().Contain("456");
        refs.Should().Contain("token-1");
        refs.Should().NotContain("gift-month");
        refs.Should().NotContain("חודש-מתנה");
    }

    [Fact]
    public void ProviderIdentifierRules_ShouldTreatSyntheticAndMarkersAsInvalid()
    {
        MembershipCancellationHelper.IsProviderToken("wh:abc123").Should().BeFalse();
        MembershipCancellationHelper.IsProviderToken("ביטול הוראת קבע").Should().BeFalse();
        MembershipCancellationHelper.IsProviderToken("94d4a5095f56521c5adbb0c906efbeb2").Should().BeTrue();

        MembershipCancellationHelper.IsProviderAsmachta("295616074").Should().BeTrue();
        MembershipCancellationHelper.IsProviderAsmachta("ABC123").Should().BeFalse();
        MembershipCancellationHelper.IsProviderAsmachta("94/56").Should().BeFalse();
    }

    [Fact]
    public void CancellationMarkerRules_ShouldRecognizeCanonicalMarker()
    {
        const string marker = "\u05d1\u05d9\u05d8\u05d5\u05dc \u05d4\u05d5\u05e8\u05d0\u05ea \u05e7\u05d1\u05e2";

        marker.Should().Be(MembershipCancellationHelper.CancellationMarkerToken);
        MembershipCancellationHelper.IsCancellationMarkerToken($" {marker} ").Should().BeTrue();
        MembershipCancellationHelper.IsProviderToken(marker).Should().BeFalse();
    }

    [Fact]
    public void SelectBestCancellationCandidate_ShouldPreferProviderCompatibleTransaction()
    {
        var candidates = new List<Transaction>
        {
            new()
            {
                Created = DateTime.UtcNow.AddMinutes(-1),
                TransactionId = null,
                TransactionToken = "wh:synthetic",
                Asmachta = "abc",
                PayerEmail = "user@example.com",
                DirectDebitId = 123
            },
            new()
            {
                Created = DateTime.UtcNow.AddMinutes(-5),
                TransactionId = 405369,
                TransactionToken = "94d4a5095f56521c5adbb0c906efbeb2",
                Asmachta = "295616074",
                PayerEmail = "user@example.com",
                DirectDebitId = 123
            }
        };

        var refs = new[] { "405369", "wh:synthetic" };
        var parsedIds = MembershipCancellationHelper.ParseTransactionIds(refs);
        var parsedTokens = MembershipCancellationHelper.ParseTransactionTokens(refs);

        var selected = MembershipCancellationHelper.SelectBestCancellationCandidate(
            candidates,
            parsedIds,
            parsedTokens,
            "user@example.com");

        selected.Should().NotBeNull();
        selected!.TransactionId.Should().Be(405369);
        selected.TransactionToken.Should().Be("94d4a5095f56521c5adbb0c906efbeb2");
    }

    [Fact]
    public void SelectProviderCancellationCandidate_ShouldRejectSyntheticOnlyTransaction()
    {
        var candidates = new List<Transaction>
        {
            new()
            {
                Created = DateTime.UtcNow,
                TransactionId = null,
                TransactionToken = "wh:synthetic",
                Asmachta = "295616074",
                PayerEmail = "user@example.com",
                DirectDebitId = 123,
                PaymentType = 1
            }
        };

        var refs = new[] { "wh:synthetic" };
        var selected = MembershipCancellationHelper.SelectProviderCancellationCandidate(
            candidates,
            MembershipCancellationHelper.ParseTransactionIds(refs),
            MembershipCancellationHelper.ParseTransactionTokens(refs),
            "user@example.com");

        selected.Should().BeNull();
    }

    [Fact]
    public void SelectProviderCancellationCandidate_ShouldPreferDirectDebitWithRequiredProviderIdentifiers()
    {
        var candidates = new List<Transaction>
        {
            new()
            {
                Created = DateTime.UtcNow,
                TransactionId = 999999,
                TransactionToken = "annualtoken123",
                Asmachta = "999999999",
                PayerEmail = "user@example.com",
                PaymentType = 2
            },
            new()
            {
                Created = DateTime.UtcNow.AddMinutes(-1),
                TransactionId = null,
                TransactionToken = "wh:synthetic",
                Asmachta = "295616074",
                PayerEmail = "user@example.com",
                DirectDebitId = 123,
                PaymentType = 1
            },
            new()
            {
                Created = DateTime.UtcNow.AddMinutes(-5),
                TransactionId = 405369,
                TransactionToken = "94d4a5095f56521c5adbb0c906efbeb2",
                Asmachta = "295616074",
                PayerEmail = "user@example.com",
                DirectDebitId = 123,
                PaymentType = 1
            }
        };

        var refs = new[] { "wh:synthetic" };
        var selected = MembershipCancellationHelper.SelectProviderCancellationCandidate(
            candidates,
            MembershipCancellationHelper.ParseTransactionIds(refs),
            MembershipCancellationHelper.ParseTransactionTokens(refs),
            "user@example.com");

        selected.Should().NotBeNull();
        selected!.TransactionId.Should().Be(405369);
        selected.TransactionToken.Should().Be("94d4a5095f56521c5adbb0c906efbeb2");
    }

    [Fact]
    public void SelectProviderCancellationCandidate_ShouldUseDirectDebitIdWhenAccountEmailChanged()
    {
        var candidates = new List<Transaction>
        {
            new()
            {
                Created = DateTime.UtcNow,
                TransactionId = null,
                TransactionToken = "wh:synthetic",
                Asmachta = "295616074",
                PayerEmail = "new@example.com",
                DirectDebitId = 123,
                PaymentType = 1
            },
            new()
            {
                Created = DateTime.UtcNow.AddMinutes(-10),
                TransactionId = 405369,
                TransactionToken = "94d4a5095f56521c5adbb0c906efbeb2",
                Asmachta = "295616074",
                PayerEmail = "old@example.com",
                DirectDebitId = 123,
                PaymentType = 1
            }
        };

        var refs = new[] { "wh:synthetic" };
        var selected = MembershipCancellationHelper.SelectProviderCancellationCandidate(
            candidates,
            MembershipCancellationHelper.ParseTransactionIds(refs),
            MembershipCancellationHelper.ParseTransactionTokens(refs),
            "new@example.com",
            new[] { 123 });

        selected.Should().NotBeNull();
        selected!.TransactionId.Should().Be(405369);
        selected.PayerEmail.Should().Be("old@example.com");
    }
}

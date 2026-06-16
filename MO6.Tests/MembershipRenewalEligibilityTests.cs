using FluentAssertions;
using MyProject12.Models;
using Xunit;

namespace MO6.Tests;

public class MembershipRenewalEligibilityTests
{
    [Fact]
    public void HasActiveMembership_CancelledMonthlyStillActive_ReturnsTrue()
    {
        var now = new DateTime(2026, 6, 15, 12, 0, 0);
        var memberships = new[]
        {
            new Membership
            {
                memberID = "member-1",
                expiration = now.AddDays(10),
                isMonthly = true,
                isMonthlyActive = false
            }
        }.AsQueryable();

        memberships.HasActiveMembership("member-1", now).Should().BeTrue();
    }

    [Fact]
    public void HasActiveMembership_ExpiredCancelledMonthly_ReturnsFalse()
    {
        var now = new DateTime(2026, 6, 15, 12, 0, 0);
        var memberships = new[]
        {
            new Membership
            {
                memberID = "member-1",
                expiration = now.AddSeconds(-1),
                isMonthly = true,
                isMonthlyActive = false
            }
        }.AsQueryable();

        memberships.HasActiveMembership("member-1", now).Should().BeFalse();
    }

    [Fact]
    public void HasActiveMembership_ActiveYearlyMembership_ReturnsTrue()
    {
        var now = new DateTime(2026, 6, 15, 12, 0, 0);
        var memberships = new[]
        {
            new Membership
            {
                memberID = "member-1",
                expiration = now.AddDays(10),
                isMonthly = false,
                isMonthlyActive = false
            }
        }.AsQueryable();

        memberships.HasActiveMembership("member-1", now).Should().BeTrue();
    }

    [Fact]
    public void HasActiveMembership_ActiveRecurringMonthlyMembership_ReturnsTrue()
    {
        var now = new DateTime(2026, 6, 15, 12, 0, 0);
        var memberships = new[]
        {
            new Membership
            {
                memberID = "member-1",
                expiration = now.AddDays(10),
                isMonthly = true,
                isMonthlyActive = true
            }
        }.AsQueryable();

        memberships.HasActiveMembership("member-1", now).Should().BeTrue();
    }

    [Fact]
    public void HasActiveMembership_OtherMembersActiveMembership_ReturnsFalse()
    {
        var now = new DateTime(2026, 6, 15, 12, 0, 0);
        var memberships = new[]
        {
            new Membership
            {
                memberID = "other-member",
                expiration = now.AddDays(10),
                isMonthly = true,
                isMonthlyActive = false
            }
        }.AsQueryable();

        memberships.HasActiveMembership("member-1", now).Should().BeFalse();
    }
}

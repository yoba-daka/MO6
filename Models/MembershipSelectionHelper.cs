using Microsoft.EntityFrameworkCore;

namespace MyProject12.Models
{
    public static class MembershipSelectionHelper
    {
        public static bool HasActiveMembership(this IQueryable<Membership> memberships, string memberId, DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return false;
            }

            var cutoff = now ?? DateTime.Now;
            return memberships.Any(x => x.memberID == memberId && x.expiration >= cutoff);
        }

        public static Membership? GetPreferredMembership(this IQueryable<Membership> memberships, string memberId, DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return null;
            }

            var cutoff = now ?? DateTime.Now;
            return memberships
                .Where(x => x.memberID == memberId)
                .OrderByDescending(x => x.expiration >= cutoff)
                .ThenByDescending(x => x.expiration)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public static Task<Membership?> GetPreferredMembershipAsync(
            this IQueryable<Membership> memberships,
            string memberId,
            CancellationToken cancellationToken,
            DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return Task.FromResult<Membership?>(null);
            }

            var cutoff = now ?? DateTime.Now;
            return memberships
                .Where(x => x.memberID == memberId)
                .OrderByDescending(x => x.expiration >= cutoff)
                .ThenByDescending(x => x.expiration)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public static Membership? GetPreferredMonthlyMembership(this IQueryable<Membership> memberships, string memberId, DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return null;
            }

            var cutoff = now ?? DateTime.Now;
            return memberships
                .Where(x => x.memberID == memberId && x.isMonthly)
                .OrderByDescending(x => x.isMonthlyActive)
                .ThenByDescending(x => x.expiration >= cutoff)
                .ThenByDescending(x => x.expiration)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
        }
    }
}

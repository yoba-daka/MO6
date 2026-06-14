using MO6.Models;
using MyProject12.Models;

namespace MyProject12.Services
{
    public class TemporaryMemberResolver
    {
        private readonly DB _db;

        public TemporaryMemberResolver(DB db)
        {
            _db = db;
        }

        public TemporaryMember? ResolveByToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var trimmed = token.Trim();
            return _db.TemporaryMembers.FirstOrDefault(x => x.Token == trimmed);
        }

        public TemporaryMember? ResolveNewestUnprocessedByExactEmail(string? email)
        {
            var normalized = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return _db.TemporaryMembers
                .Where(x => !x.Processed && !string.IsNullOrWhiteSpace(x.Email))
                .AsEnumerable()
                .Where(x => string.Equals(NormalizeEmail(x.Email), normalized, StringComparison.Ordinal))
                .OrderByDescending(x => x.Created)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public TemporaryMember? ResolveSafeAutoHealCandidate(Transaction? transaction, TimeSpan maxAge)
        {
            if (transaction == null)
            {
                return null;
            }

            var normalizedEmail = NormalizeEmail(transaction.PayerEmail);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            return _db.TemporaryMembers
                .Where(x => !x.Processed && !string.IsNullOrWhiteSpace(x.Email))
                .AsEnumerable()
                .Where(x => string.Equals(NormalizeEmail(x.Email), normalizedEmail, StringComparison.Ordinal))
                .Where(x => x.Created <= transaction.Created && transaction.Created - x.Created <= maxAge)
                .Where(x => MatchesAutoHealIdentity(transaction, x))
                .OrderByDescending(x => x.Created)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
        }

        public static string NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? string.Empty
                : email.Trim().ToLowerInvariant();
        }

        private static bool MatchesAutoHealIdentity(Transaction transaction, TemporaryMember candidate)
        {
            var transactionPhone = NormalizePhone(transaction.PayerPhone);
            var candidatePhone = NormalizePhone(candidate.Phone);
            if (!string.IsNullOrWhiteSpace(transactionPhone) && !string.IsNullOrWhiteSpace(candidatePhone))
            {
                return string.Equals(transactionPhone, candidatePhone, StringComparison.Ordinal);
            }

            var transactionName = NormalizeName(transaction.FullName);
            var candidateName = NormalizeName(candidate.Name);
            if (!string.IsNullOrWhiteSpace(transactionName) && !string.IsNullOrWhiteSpace(candidateName))
            {
                return string.Equals(transactionName, candidateName, StringComparison.Ordinal);
            }

            return false;
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return string.Empty;
            }

            return new string(phone.Where(char.IsDigit).ToArray());
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return string.Join(" ", name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

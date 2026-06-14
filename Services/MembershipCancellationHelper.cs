using MyProject12.Models;
using System.Text.RegularExpressions;

namespace MyProject12.Services
{
    public static class MembershipCancellationHelper
    {
        public const string CancellationMarkerToken = "\u05d1\u05d9\u05d8\u05d5\u05dc \u05d4\u05d5\u05e8\u05d0\u05ea \u05e7\u05d1\u05e2";

        private static readonly Regex ProviderTokenRegex = new("^[A-Za-z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ProviderAsmachtaRegex = new("^[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly char[] TrimChars = { '"', '\'', '[', ']', '(', ')', '{', '}' };

        public static List<string> ParseTransactionReferences(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var separators = new[] { ';', ',', '\n', '\r', '\t', '|' };

            return raw
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Trim(TrimChars))
                .Where(x => x.Length > 0 && !IsNonTransactionReferenceMarker(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static HashSet<int> ParseTransactionIds(IEnumerable<string> refs)
        {
            return refs
                .Select(x => int.TryParse(x, out var parsed) ? parsed : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();
        }

        public static HashSet<string> ParseTransactionTokens(IEnumerable<string> refs)
        {
            return refs
                .Where(x => !int.TryParse(x, out _))
                .Select(NormalizeComparisonValue)
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static string NormalizeComparisonValue(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return raw.Trim().Trim(TrimChars).ToLowerInvariant();
        }

        public static bool IsCancellationMarkerToken(string? token)
        {
            return string.Equals(token?.Trim(), CancellationMarkerToken, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSyntheticWebhookToken(string? token)
        {
            return !string.IsNullOrWhiteSpace(token) &&
                   token.StartsWith("wh:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsProviderToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                IsSyntheticWebhookToken(token) ||
                IsCancellationMarkerToken(token))
            {
                return false;
            }

            return ProviderTokenRegex.IsMatch(token.Trim());
        }

        public static bool IsProviderAsmachta(string? asmachta)
        {
            if (string.IsNullOrWhiteSpace(asmachta))
            {
                return false;
            }

            return ProviderAsmachtaRegex.IsMatch(asmachta.Trim());
        }

        public static bool HasProviderIdentifier(Transaction? transaction)
        {
            if (transaction == null)
            {
                return false;
            }

            return (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0) ||
                   IsProviderToken(transaction.TransactionToken) ||
                   IsProviderAsmachta(transaction.Asmachta) ||
                   (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0);
        }

        public static bool IsDirectDebitCandidate(Transaction? transaction)
        {
            if (transaction == null)
            {
                return false;
            }

            return (transaction.DirectDebitId.HasValue && transaction.DirectDebitId.Value > 0) ||
                   transaction.PaymentType == 1;
        }

        public static bool HasRequiredUpdateDirectDebitIdentifiers(Transaction? transaction)
        {
            if (transaction == null)
            {
                return false;
            }

            return transaction.TransactionId.HasValue &&
                   transaction.TransactionId.Value > 0 &&
                   IsProviderToken(transaction.TransactionToken) &&
                   IsProviderAsmachta(transaction.Asmachta);
        }

        public static Transaction? SelectProviderCancellationCandidate(
            IEnumerable<Transaction> candidates,
            HashSet<int> parsedTransactionIds,
            HashSet<string> parsedTransactionTokens,
            string? email,
            IEnumerable<int>? directDebitIds = null)
        {
            if (candidates == null)
            {
                return null;
            }

            var normalizedEmail = NormalizeComparisonValue(email);
            var directDebitIdSet = (directDebitIds ?? Enumerable.Empty<int>()).ToHashSet();

            return candidates
                .Where(x => x != null && !IsCancellationMarkerToken(x.TransactionToken))
                .Where(IsDirectDebitCandidate)
                .Where(HasRequiredUpdateDirectDebitIdentifiers)
                .Where(x =>
                    (x.TransactionId.HasValue && parsedTransactionIds.Contains(x.TransactionId.Value)) ||
                    (!string.IsNullOrWhiteSpace(x.TransactionToken) &&
                     parsedTransactionTokens.Contains(NormalizeComparisonValue(x.TransactionToken))) ||
                    (x.DirectDebitId.HasValue && directDebitIdSet.Contains(x.DirectDebitId.Value)) ||
                    (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                     NormalizeComparisonValue(x.PayerEmail) == normalizedEmail))
                .OrderByDescending(x => x.TransactionId.HasValue && parsedTransactionIds.Contains(x.TransactionId.Value))
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.TransactionToken) &&
                                       parsedTransactionTokens.Contains(NormalizeComparisonValue(x.TransactionToken)))
                .ThenByDescending(x => x.DirectDebitId.HasValue && directDebitIdSet.Contains(x.DirectDebitId.Value))
                .ThenByDescending(x => x.DirectDebitId.HasValue && x.DirectDebitId.Value > 0)
                .ThenByDescending(x => x.Created)
                .FirstOrDefault();
        }

        public static Transaction? SelectBestCancellationCandidate(
            IEnumerable<Transaction> candidates,
            HashSet<int> parsedTransactionIds,
            HashSet<string> parsedTransactionTokens,
            string? email)
        {
            if (candidates == null)
            {
                return null;
            }

            var normalizedEmail = NormalizeComparisonValue(email);

            return candidates
                .Where(x => x != null && !IsCancellationMarkerToken(x.TransactionToken))
                .Where(x =>
                    (x.TransactionId.HasValue && parsedTransactionIds.Contains(x.TransactionId.Value)) ||
                    (!string.IsNullOrWhiteSpace(x.TransactionToken) &&
                     parsedTransactionTokens.Contains(NormalizeComparisonValue(x.TransactionToken))) ||
                    (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                     NormalizeComparisonValue(x.PayerEmail) == normalizedEmail))
                .OrderByDescending(x => HasProviderIdentifier(x))
                .ThenByDescending(x => x.DirectDebitId.HasValue && x.DirectDebitId.Value > 0)
                .ThenByDescending(x => x.TransactionId.HasValue && x.TransactionId.Value > 0)
                .ThenByDescending(x => IsProviderToken(x.TransactionToken))
                .ThenByDescending(x => IsProviderAsmachta(x.Asmachta))
                .ThenByDescending(x => x.Created)
                .FirstOrDefault();
        }

        private static bool IsNonTransactionReferenceMarker(string value)
        {
            var normalized = NormalizeComparisonValue(value);
            return normalized == "חודש-מתנה" ||
                   normalized == "gift-month" ||
                   normalized == "giftmonth";
        }
    }
}

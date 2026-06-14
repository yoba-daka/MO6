namespace MyProject12.Services
{
    public static class MeshulamApiUrl
    {
        public static string NormalizeBaseAddress(string? baseAddress)
        {
            var normalized = (baseAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Meshulam base address is not configured.");
            }

            if (!normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized += "/";
            }

            return normalized;
        }
    }
}

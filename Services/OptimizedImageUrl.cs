using System;
using System.Collections.Generic;
using System.Linq;

namespace MyProject12.Services
{
    public static class OptimizedImageUrl
    {
        public const int DefaultQuality = 80;
        private static readonly HashSet<string> ConvertibleExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".bmp",
            ".tif",
            ".tiff"
        };

        public static string ApplyWebpDefaults(string imageUrl, int quality = DefaultQuality)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return imageUrl ?? string.Empty;
            }

            if (!ShouldApplyWebpDefaults(imageUrl))
            {
                return imageUrl;
            }

            quality = Math.Clamp(quality, 1, 100);

            var fragmentIndex = imageUrl.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? imageUrl[fragmentIndex..] : string.Empty;
            var withoutFragment = fragmentIndex >= 0 ? imageUrl[..fragmentIndex] : imageUrl;

            var queryIndex = withoutFragment.IndexOf('?');
            var path = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
            var query = queryIndex >= 0 ? withoutFragment[(queryIndex + 1)..] : string.Empty;

            var queryParts = SplitQuery(query)
                .Where(part => !HasKey(part, "format") && !HasKey(part, "quality"))
                .ToList();

            queryParts.Add("format=webp");
            queryParts.Add($"quality={quality}");

            return $"{path}?{string.Join("&", queryParts)}{fragment}";
        }

        private static bool ShouldApplyWebpDefaults(string imageUrl)
        {
            var path = GetPath(imageUrl);
            if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ConvertibleExtensions.Contains(GetExtension(path));
        }

        private static string GetPath(string imageUrl)
        {
            var withoutFragment = imageUrl.Split('#', 2)[0];
            return withoutFragment.Split('?', 2)[0];
        }

        private static string GetExtension(string path)
        {
            var slashIndex = path.LastIndexOf('/');
            var fileName = slashIndex >= 0 ? path[(slashIndex + 1)..] : path;
            var dotIndex = fileName.LastIndexOf('.');

            return dotIndex >= 0 ? fileName[dotIndex..] : string.Empty;
        }

        private static IEnumerable<string> SplitQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<string>();
            }

            return query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool HasKey(string queryPart, string key)
        {
            var equalsIndex = queryPart.IndexOf('=');
            var partKey = equalsIndex >= 0 ? queryPart[..equalsIndex] : queryPart;
            return string.Equals(Uri.UnescapeDataString(partKey), key, StringComparison.OrdinalIgnoreCase);
        }
    }
}

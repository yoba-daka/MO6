namespace MO6.Middleware
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Umbraco.Cms.Core.Services;

    public class CustomRedirectMiddleware
    {
        private const string RedirectRulesCacheKey = "custom-redirect-rules-v1";
        private static readonly TimeSpan RedirectRulesCacheDuration = TimeSpan.FromMinutes(5);

        private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/",
            "/מאמרים",
            "/אודות",
            "/אירועים",
            "/ספרים",
            "/צור-קשר",
            "/קורסים-והרצאות",
            "/חשבון",
            "/חיפוש"
        };

        private static readonly string[] StaticPrefixes =
        {
            "/media/",
            "/css/",
            "/js/",
            "/fonts/",
            "/images/",
            "/img/",
            "/umbraco/",
            "/lib/",
            "/dist/"
        };

        private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".svg", ".ico", ".bmp",
            ".css", ".js", ".map", ".woff", ".woff2", ".ttf", ".eot", ".otf",
            ".mp3", ".mp4", ".m3u8", ".ts", ".pdf", ".xml", ".txt", ".json", ".webmanifest"
        };

        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        public CustomRedirectMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var method = context.Request.Method;
            if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            {
                await _next(context);
                return;
            }

            var normalizedPath = NormalizePath(context.Request.Path.Value);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                await _next(context);
                return;
            }

            if (ShouldBypassRequest(context, normalizedPath))
            {
                await _next(context);
                return;
            }

            // Never apply CMS-managed redirects to payment/webhook/harness endpoints.
            if (normalizedPath.StartsWith("/payments-harness", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Equals("/meshulam-response", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Equals("/meshulam-dd-success", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Equals("/meshulam-dd-failure", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (!ExcludedPaths.Contains(normalizedPath))
            {
                var redirectRules = GetRedirectRules(context);
                if (redirectRules.TryGetValue(normalizedPath, out var toPath) && !string.IsNullOrWhiteSpace(toPath))
                {
                    context.Response.Redirect(toPath, true);
                    return;
                }
            }

            await _next(context);
        }

        private bool ShouldBypassRequest(HttpContext context, string path)
        {
            if (StaticPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var extension = Path.GetExtension(path);
            if (!string.IsNullOrWhiteSpace(extension) && StaticExtensions.Contains(extension))
            {
                return true;
            }

            var accept = context.Request.Headers["Accept"].ToString();
            if (!string.IsNullOrWhiteSpace(accept) &&
                !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private IReadOnlyDictionary<string, string> GetRedirectRules(HttpContext context)
        {
            return _cache.GetOrCreate(RedirectRulesCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = RedirectRulesCacheDuration;

                var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var contentService = context.RequestServices.GetRequiredService<IContentService>();
                    var listViewItems = contentService.GetPagedChildren(4267, 0, int.MaxValue, out _);

                    foreach (var item in listViewItems)
                    {
                        var fromPath = NormalizePath(item.GetValue<string>("From"));
                        var toPath = item.GetValue<string>("To")?.Trim();
                        if (string.IsNullOrWhiteSpace(fromPath) || string.IsNullOrWhiteSpace(toPath))
                        {
                            continue;
                        }

                        if (!rules.ContainsKey(fromPath))
                        {
                            rules[fromPath] = toPath;
                        }
                    }
                }
                catch
                {
                    // Fail open: if redirect rules cannot be read, keep request flow alive.
                }

                return rules;
            }) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            var path = rawPath.Trim();
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            path = path.ToLowerInvariant();
            if (path.Length > 1)
            {
                path = path.TrimEnd('/');
            }

            return path;
        }
    }

    public static class CustomRedirectMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomRedirectMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CustomRedirectMiddleware>();
        }
    }
}

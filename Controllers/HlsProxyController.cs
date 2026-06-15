using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyProject12.Models;
using System.Net;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace MyProject12.Controllers
{
    public class HlsProxyController : Controller
    {
        private static readonly Regex PlaylistUriAttributeRegex = new(@"\bURI=""(?<uri>[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly TimeSpan LessonMetadataCacheDuration = TimeSpan.FromMinutes(5);

        private readonly LessonBlobService _blobService;
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IMemberManager _memberManager;
        private readonly DB _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HlsProxyController> _logger;

        public HlsProxyController(
            LessonBlobService blobService,
            IUmbracoContextFactory umbracoContextFactory,
            IMemberManager memberManager,
            DB db,
            IMemoryCache cache,
            ILogger<HlsProxyController> logger)
        {
            _blobService = blobService;
            _umbracoContextFactory = umbracoContextFactory;
            _memberManager = memberManager;
            _db = db;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string contentName, string? blobPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contentName))
            {
                return BadRequest();
            }

            blobPath = NormalizeBlobPath(string.IsNullOrWhiteSpace(blobPath) ? "index.m3u8" : blobPath);
            if (blobPath == null)
            {
                return BadRequest();
            }

            var lesson = GetLessonMetadata(contentName);
            if (lesson == null)
            {
                return NotFound();
            }

            if (!IsPlaylist(blobPath))
            {
                return NotFound();
            }

            var access = await ResolvePlaylistAccessAsync(lesson, cancellationToken);
            if (!access.Allowed)
            {
                return lesson.MembersOnly ? StatusCode(StatusCodes.Status403Forbidden) : Unauthorized();
            }

            try
            {
                var blobClient = _blobService.GetBlobClient(lesson.ContentName, blobPath);
                if (!await blobClient.ExistsAsync(cancellationToken))
                {
                    return NotFound();
                }

                var download = await blobClient.DownloadContentAsync(cancellationToken);
                var playlist = download.Value.Content.ToString();
                var rewritten = RewritePlaylist(lesson.ContentName, blobPath, playlist, access.SasToken);

                Response.Headers.CacheControl = lesson.MembersOnly
                    ? "private, no-store"
                    : "public, max-age=60";

                return Content(rewritten, "application/vnd.apple.mpegurl; charset=utf-8");
            }
            catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }
            catch (RequestFailedException ex)
            {
                _logger.LogWarning(ex, "HLS proxy blob request failed for {ContentName}/{BlobPath}", contentName, blobPath);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private LessonMetadata? GetLessonMetadata(string contentName)
        {
            return _cache.GetOrCreate($"hls-lesson:{contentName}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = LessonMetadataCacheDuration;

                using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();
                var contentCache = umbracoContextReference.UmbracoContext.Content;
                var lesson = contentCache?
                    .GetAtRoot()
                    .SelectMany(root => root.DescendantsOrSelf())
                    .FirstOrDefault(content =>
                        content.ContentType.Alias == "lesson" &&
                        string.Equals(content.Value<string>("contentName"), contentName, StringComparison.OrdinalIgnoreCase));

                if (lesson == null)
                {
                    return null;
                }

                return new LessonMetadata(
                    Id: lesson.Id,
                    ContentName: lesson.Value<string>("contentName") ?? contentName,
                    MembersOnly: lesson.Value<bool>("membersOnly"));
            });
        }

        private async Task<PlaylistAccess> ResolvePlaylistAccessAsync(LessonMetadata lesson, CancellationToken cancellationToken)
        {
            if (!lesson.MembersOnly)
            {
                return new PlaylistAccess(true, _blobService.GenerateUnlimitedSas(lesson.ContentName, "*"));
            }

            if (!_memberManager.IsLoggedIn())
            {
                return PlaylistAccess.Denied;
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member?.Id == null)
            {
                return PlaylistAccess.Denied;
            }

            var membership = await _db.Memberships
                .AsNoTracking()
                .GetPreferredMembershipAsync(member.Id, cancellationToken);
            if (membership == null || membership.expiration < DateTime.Now)
            {
                return PlaylistAccess.Denied;
            }

            var sasToken = _blobService.GenerateMemberContainerSas(lesson.ContentName, member.Id, membership.expiration);
            return string.IsNullOrWhiteSpace(sasToken)
                ? PlaylistAccess.Denied
                : new PlaylistAccess(true, sasToken);
        }

        private string RewritePlaylist(string contentName, string playlistPath, string playlist, string sasToken)
        {
            var lines = playlist.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    lines[i] = line;
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    lines[i] = PlaylistUriAttributeRegex.Replace(line, match =>
                    {
                        var uri = match.Groups["uri"].Value;
                        return $"URI=\"{BuildPlaylistReferenceUrl(contentName, playlistPath, uri, sasToken)}\"";
                    });
                    continue;
                }

                lines[i] = BuildPlaylistReferenceUrl(contentName, playlistPath, trimmed, sasToken);
            }

            return string.Join('\n', lines);
        }

        private string BuildPlaylistReferenceUrl(string contentName, string currentBlobPath, string reference, string sasToken)
        {
            var resolvedPath = ResolveReferencedBlobPath(contentName, currentBlobPath, reference);
            if (resolvedPath == null)
            {
                return reference;
            }

            if (IsPlaylist(resolvedPath))
            {
                return $"/media/hls/{Uri.EscapeDataString(contentName)}/{EncodeBlobPath(resolvedPath)}";
            }

            var blobUrl = _blobService.GenerateBlobUrl(contentName, resolvedPath, sasToken);
            return string.IsNullOrWhiteSpace(blobUrl) ? reference : blobUrl;
        }

        private static string? ResolveReferencedBlobPath(string contentName, string currentBlobPath, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (Uri.TryCreate(reference, UriKind.Absolute, out var absoluteReference))
            {
                if (!string.Equals(absoluteReference.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(absoluteReference.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var absolutePath = WebUtility.UrlDecode(absoluteReference.AbsolutePath.TrimStart('/'));
                if (absolutePath.StartsWith(contentName + "/", StringComparison.OrdinalIgnoreCase))
                {
                    absolutePath = absolutePath[(contentName.Length + 1)..];
                    return NormalizeBlobPath(absolutePath);
                }

                return null;
            }

            var referencePath = WebUtility.UrlDecode(reference.Split('?', '#')[0]);
            if (referencePath.StartsWith("/", StringComparison.Ordinal))
            {
                referencePath = referencePath.TrimStart('/');
                if (referencePath.StartsWith(contentName + "/", StringComparison.OrdinalIgnoreCase))
                {
                    referencePath = referencePath[(contentName.Length + 1)..];
                }

                return NormalizeBlobPath(referencePath);
            }

            var currentDirectory = GetDirectory(currentBlobPath);
            return NormalizeBlobPath($"{currentDirectory}{referencePath}");
        }

        private static string? NormalizeBlobPath(string? blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return null;
            }

            blobPath = blobPath.Replace('\\', '/').TrimStart('/');
            var parts = new List<string>();
            foreach (var part in blobPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".")
                {
                    continue;
                }

                if (part == "..")
                {
                    if (parts.Count == 0)
                    {
                        return null;
                    }

                    parts.RemoveAt(parts.Count - 1);
                    continue;
                }

                parts.Add(part);
            }

            return parts.Count == 0 ? null : string.Join('/', parts);
        }

        private static string GetDirectory(string blobPath)
        {
            var index = blobPath.LastIndexOf('/');
            return index < 0 ? string.Empty : blobPath[..(index + 1)];
        }

        private static string EncodeBlobPath(string blobPath)
        {
            return string.Join('/', blobPath.Split('/').Select(Uri.EscapeDataString));
        }

        private static bool IsPlaylist(string blobPath)
        {
            return blobPath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record PlaylistAccess(bool Allowed, string SasToken)
        {
            public static readonly PlaylistAccess Denied = new(false, string.Empty);
        }

        private sealed record LessonMetadata(int Id, string ContentName, bool MembersOnly);
    }
}

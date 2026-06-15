using Azure;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyProject12.Models;
using System.Net;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Extensions;

namespace MyProject12.Controllers
{
    public class HlsProxyController : Controller
    {
        private static readonly Regex PlaylistUriAttributeRegex = new(@"\bURI=""(?<uri>[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly TimeSpan LessonMetadataCacheDuration = TimeSpan.FromMinutes(5);

        private readonly LessonBlobService _blobService;
        private readonly IPublishedContentQuery _contentQuery;
        private readonly IMemberManager _memberManager;
        private readonly DB _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HlsProxyController> _logger;

        public HlsProxyController(
            LessonBlobService blobService,
            IPublishedContentQuery contentQuery,
            IMemberManager memberManager,
            DB db,
            IMemoryCache cache,
            ILogger<HlsProxyController> logger)
        {
            _blobService = blobService;
            _contentQuery = contentQuery;
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

            if (!await IsAuthorizedForLessonAsync(lesson, cancellationToken))
            {
                return lesson.MembersOnly ? StatusCode(StatusCodes.Status403Forbidden) : Unauthorized();
            }

            try
            {
                var blobClient = _blobService.GetBlobClient(contentName, blobPath);
                if (!await blobClient.ExistsAsync(cancellationToken))
                {
                    return NotFound();
                }

                if (IsPlaylist(blobPath))
                {
                    var download = await blobClient.DownloadContentAsync(cancellationToken);
                    var playlist = download.Value.Content.ToString();
                    var rewritten = RewritePlaylist(contentName, blobPath, playlist);

                    Response.Headers.CacheControl = lesson.MembersOnly
                        ? "private, no-store"
                        : "public, max-age=60";

                    return Content(rewritten, "application/vnd.apple.mpegurl; charset=utf-8");
                }

                return await StreamBlobAsync(blobPath, blobClient, lesson.MembersOnly, cancellationToken);
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

                var lesson = _contentQuery
                    .ContentAtRoot()
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

        private async Task<bool> IsAuthorizedForLessonAsync(LessonMetadata lesson, CancellationToken cancellationToken)
        {
            if (!lesson.MembersOnly)
            {
                return true;
            }

            if (!_memberManager.IsLoggedIn())
            {
                return false;
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member?.Id == null)
            {
                return false;
            }

            return await _db.Memberships
                .AsNoTracking()
                .AnyAsync(x => x.memberID == member.Id && x.expiration >= DateTime.Now, cancellationToken);
        }

        private async Task<IActionResult> StreamBlobAsync(string blobPath, Azure.Storage.Blobs.BlobClient blobClient, bool isProtected, CancellationToken cancellationToken)
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var totalLength = properties.Value.ContentLength;
            var contentType = GetContentType(blobPath, properties.Value.ContentType);

            Response.Headers.AcceptRanges = "bytes";
            Response.Headers.CacheControl = isProtected
                ? "private, no-store"
                : "public, max-age=300";

            if (TryParseRange(Request.Headers.Range.ToString(), totalLength, out var start, out var end))
            {
                var length = end - start + 1;
                var download = await blobClient.DownloadStreamingAsync(
                    new BlobDownloadOptions { Range = new HttpRange(start, length) },
                    cancellationToken);

                Response.StatusCode = StatusCodes.Status206PartialContent;
                Response.Headers.ContentRange = $"bytes {start}-{end}/{totalLength}";
                Response.ContentLength = length;

                return File(download.Value.Content, contentType, enableRangeProcessing: false);
            }

            if (!string.IsNullOrWhiteSpace(Request.Headers.Range))
            {
                Response.Headers.ContentRange = $"bytes */{totalLength}";
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            var fullDownload = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            Response.ContentLength = totalLength;
            return File(fullDownload.Value.Content, contentType, enableRangeProcessing: false);
        }

        private string RewritePlaylist(string contentName, string playlistPath, string playlist)
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
                        return $"URI=\"{BuildProxyUrl(contentName, playlistPath, uri)}\"";
                    });
                    continue;
                }

                lines[i] = BuildProxyUrl(contentName, playlistPath, trimmed);
            }

            return string.Join('\n', lines);
        }

        private string BuildProxyUrl(string contentName, string currentBlobPath, string reference)
        {
            var resolvedPath = ResolveReferencedBlobPath(contentName, currentBlobPath, reference);
            if (resolvedPath == null)
            {
                return reference;
            }

            return $"/media/hls/{Uri.EscapeDataString(contentName)}/{EncodeBlobPath(resolvedPath)}";
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

        private static bool TryParseRange(string? rangeHeader, long totalLength, out long start, out long end)
        {
            start = 0;
            end = totalLength - 1;

            if (string.IsNullOrWhiteSpace(rangeHeader))
            {
                return false;
            }

            var match = Regex.Match(rangeHeader, @"^bytes=(\d*)-(\d*)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            var startValue = match.Groups[1].Value;
            var endValue = match.Groups[2].Value;

            if (string.IsNullOrEmpty(startValue))
            {
                if (!long.TryParse(endValue, out var suffixLength) || suffixLength <= 0)
                {
                    return false;
                }

                start = Math.Max(totalLength - suffixLength, 0);
                end = totalLength - 1;
                return totalLength > 0;
            }

            if (!long.TryParse(startValue, out start))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(endValue) && !long.TryParse(endValue, out end))
            {
                return false;
            }

            if (string.IsNullOrEmpty(endValue) || end >= totalLength)
            {
                end = totalLength - 1;
            }

            return start >= 0 && start < totalLength && end >= start;
        }

        private static string GetContentType(string blobPath, string? blobContentType)
        {
            if (!string.IsNullOrWhiteSpace(blobContentType) &&
                !string.Equals(blobContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return blobContentType;
            }

            return Path.GetExtension(blobPath).ToLowerInvariant() switch
            {
                ".m3u8" => "application/vnd.apple.mpegurl",
                ".ts" => "video/mp2t",
                ".m4s" => "video/iso.segment",
                ".mp4" => "video/mp4",
                ".m4a" => "audio/mp4",
                ".aac" => "audio/aac",
                ".mp3" => "audio/mpeg",
                ".vtt" or ".webvtt" => "text/vtt",
                ".key" => "application/octet-stream",
                _ => "application/octet-stream"
            };
        }

        private sealed record LessonMetadata(int Id, string ContentName, bool MembersOnly);
    }
}

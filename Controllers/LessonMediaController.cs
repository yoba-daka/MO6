using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyProject12.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace MyProject12.Controllers
{
    public class LessonMediaController : Controller
    {
        private static readonly TimeSpan LessonMetadataCacheDuration = TimeSpan.FromMinutes(5);

        private readonly LessonBlobService _blobService;
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IMemberManager _memberManager;
        private readonly DB _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LessonMediaController> _logger;

        public LessonMediaController(
            LessonBlobService blobService,
            IUmbracoContextFactory umbracoContextFactory,
            IMemberManager memberManager,
            DB db,
            IMemoryCache cache,
            ILogger<LessonMediaController> logger)
        {
            _blobService = blobService;
            _umbracoContextFactory = umbracoContextFactory;
            _memberManager = memberManager;
            _db = db;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Audio(string contentName, string? blobPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(contentName))
            {
                return BadRequest();
            }

            var audioBlobName = NormalizeAudioBlobName(blobPath);
            if (audioBlobName == null)
            {
                return NotFound();
            }

            var lesson = GetLessonMetadata(contentName);
            if (lesson == null)
            {
                return NotFound();
            }

            try
            {
                var blobClient = _blobService.GetBlobClient(lesson.ContentName, audioBlobName);
                if (!await blobClient.ExistsAsync(cancellationToken))
                {
                    return NotFound();
                }

                var access = await ResolveAudioAccessAsync(lesson, audioBlobName, cancellationToken);
                if (!access.Allowed)
                {
                    return lesson.MembersOnly ? StatusCode(StatusCodes.Status403Forbidden) : Unauthorized();
                }

                var blobUrl = _blobService.GenerateBlobUrl(lesson.ContentName, audioBlobName, access.SasToken);
                if (string.IsNullOrWhiteSpace(blobUrl))
                {
                    return StatusCode(StatusCodes.Status502BadGateway);
                }

                Response.Headers.CacheControl = "private, no-store, max-age=0";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";

                return Redirect(blobUrl);
            }
            catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }
            catch (RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Audio redirect failed for {ContentName}/{BlobPath}", contentName, audioBlobName);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private async Task<AudioAccess> ResolveAudioAccessAsync(LessonMetadata lesson, string audioBlobName, CancellationToken cancellationToken)
        {
            if (string.Equals(audioBlobName, "audio-short.mp3", StringComparison.OrdinalIgnoreCase))
            {
                return new AudioAccess(true, _blobService.GenerateUnlimitedSas(lesson.ContentName, audioBlobName));
            }

            if (!lesson.MembersOnly)
            {
                return new AudioAccess(true, _blobService.GenerateUnlimitedSas(lesson.ContentName, audioBlobName));
            }

            if (!_memberManager.IsLoggedIn())
            {
                return AudioAccess.Denied;
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member?.Id == null)
            {
                return AudioAccess.Denied;
            }

            var membership = await _db.Memberships
                .AsNoTracking()
                .GetPreferredMembershipAsync(member.Id, cancellationToken);
            if (membership == null || membership.expiration < DateTime.Now)
            {
                return AudioAccess.Denied;
            }

            var sasToken = _blobService.GenerateMemberContainerSas(lesson.ContentName, member.Id, membership.expiration);
            return string.IsNullOrWhiteSpace(sasToken)
                ? AudioAccess.Denied
                : new AudioAccess(true, sasToken);
        }

        private LessonMetadata? GetLessonMetadata(string contentName)
        {
            return _cache.GetOrCreate($"lesson-media:{contentName}", entry =>
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
                    ContentName: lesson.Value<string>("contentName") ?? contentName,
                    MembersOnly: lesson.Value<bool>("membersOnly"));
            });
        }

        private static string? NormalizeAudioBlobName(string? blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return "audio.mp3";
            }

            var normalized = blobPath.Replace('\\', '/').TrimStart('/');
            return normalized.Equals("audio.mp3", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("audio-short.mp3", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : null;
        }

        private sealed record AudioAccess(bool Allowed, string SasToken)
        {
            public static readonly AudioAccess Denied = new(false, string.Empty);
        }

        private sealed record LessonMetadata(string ContentName, bool MembersOnly);
    }
}

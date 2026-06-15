using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MyProject12.Models;
using System.Globalization;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;

namespace MyProject12.Controllers
{
    public class TimeKeeperController : SurfaceController
    {
        private readonly IMemberManager _memberManager;
        private readonly DB _db; // Assuming you have a DB context for Entity Framework Core

        public TimeKeeperController(IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider, DB db, IMemberManager memberManager)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)

        {
            _memberManager = memberManager;
            _db = db;
        }
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        [HttpPost]
        public async Task<IActionResult> Set(int lessonId, string time, bool isVideo, long? clientUpdatedAt, CancellationToken cancellationToken)
        {
            if (lessonId <= 0 || string.IsNullOrWhiteSpace(time))
            {
                return BadRequest("Invalid input parameters.");
            }

            if (!double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                double.IsNaN(seconds) ||
                double.IsInfinity(seconds))
            {
                return BadRequest("Invalid time value.");
            }

            var normalizedTime = Math.Max(0, seconds).ToString("0.###", CultureInfo.InvariantCulture);

            if (!_memberManager.IsLoggedIn())
            {
                return Unauthorized();
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null || member.Id == null)
            {
                return Unauthorized();
            }

            var hasValidClientUpdatedUtc = TryResolveClientUpdatedUtc(clientUpdatedAt, out var clientUpdatedUtc);
            var timeKeeper = _db.TimeKeepers
                                 .FirstOrDefault(x => x.memberID == member.Id && x.lessonId == lessonId);

            if (timeKeeper == null)
            {
                if (!hasValidClientUpdatedUtc)
                {
                    clientUpdatedUtc = DateTime.UtcNow;
                }

                _db.TimeKeepers.Add(new TimeKeeper { lessonId = lessonId, time = normalizedTime, memberID = member.Id, isVideo = isVideo, date = clientUpdatedUtc });
            }
            else
            {
                if (!hasValidClientUpdatedUtc)
                {
                    return Ok();
                }

                if (timeKeeper.date > clientUpdatedUtc.AddSeconds(1))
                {
                    return Ok();
                }

                timeKeeper.isVideo = isVideo;
                timeKeeper.time = normalizedTime;
                timeKeeper.date = clientUpdatedUtc;
                _db.TimeKeepers.Update(timeKeeper);
            }

            await _db.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        private static bool TryResolveClientUpdatedUtc(long? clientUpdatedAt, out DateTime clientUpdatedUtc)
        {
            var now = DateTime.UtcNow;
            clientUpdatedUtc = now;

            if (!clientUpdatedAt.HasValue || clientUpdatedAt.Value <= 0)
            {
                return false;
            }

            try
            {
                var resolvedClientUpdatedUtc = DateTimeOffset.FromUnixTimeMilliseconds(clientUpdatedAt.Value).UtcDateTime;
                if (resolvedClientUpdatedUtc < now.AddDays(-30) || resolvedClientUpdatedUtc > now.AddMinutes(10))
                {
                    return false;
                }

                clientUpdatedUtc = resolvedClientUpdatedUtc;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}

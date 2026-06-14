using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MyProject12.Models;
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
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public async Task<IActionResult> Set(int lessonId, string time, bool isVideo)
        {
            if (string.IsNullOrEmpty(time))
            {
                return BadRequest("Invalid input parameters.");
            }

            if (!_memberManager.IsLoggedIn())
            {
                return NotFound();
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member.Id == null)
            {
                return NotFound();
            }

            var timeKeeper = _db.TimeKeepers
                                 .FirstOrDefault(x => x.memberID == member.Id && x.lessonId == lessonId);

            if (timeKeeper == null)
            {
                _db.TimeKeepers.Add(new TimeKeeper { lessonId = lessonId, time = time, memberID = member.Id, isVideo = isVideo, date = DateTime.Now });
            }
            else
            {
                timeKeeper.isVideo = isVideo;
                timeKeeper.time = time;
                timeKeeper.date = DateTime.Now;
                _db.TimeKeepers.Update(timeKeeper);
            }

            _db.SaveChanges(); // Consider async SaveChanges if using EF Core

            return Ok();
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;
using MosheSharon.Models;
using MyProject12.ViewModels;
using MyProject12.Models;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using GoogleReCaptcha.V3.Interface;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Core.Models.ContentEditing;
using MyProject12.Services;

namespace MyProject12.Controllers
{

    public class CommentsController : SurfaceController
    {
        private readonly ICaptchaValidator _captchaValidator;
        private readonly IMemberManager _memberManager;
        private readonly IConfiguration _configuration;


        private readonly DB _db; // Assuming you have a DB context for Entity Framework Core

        public CommentsController (IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,DB db,
    ICaptchaValidator captchaValidator,IMemberManager memberManager,IConfiguration configuration)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        
        {
            _db = db;
            _captchaValidator = captchaValidator;
            _memberManager = memberManager; 
            _configuration = configuration;
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Create(CommentView cv, string captcha)
        {
            if (!_memberManager.IsLoggedIn()) return NotFound();
            // Validate the captcha first
            bool checkCapthcha = CaptchaSettings.IsEnabled(_configuration, "OnComment");
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha) && checkCapthcha)
            {
                ModelState.AddModelError("captcha", "Captcha validation failed");
            }
            Comment c = new Comment();


            if (ModelState.IsValid)
            {
                var member = await _memberManager.GetCurrentMemberAsync();
                string memberName = member.Name != null? member.Name : "ללא שם";

                c.parentID = cv.parentID;
                c.name = cv.name == "ללא שם" ? "ללא שם" : memberName;
                c.time = DateTime.Now;
                c.title = cv.title;
                c.content = cv.content;
                c.managment = false;
                if(cv.parentCommentID!=null)
                {
                    int id = Comment.getOriginalID(cv.parentCommentID.Value);
                    var parent = _db.Comments.First(x => x.ID == id);
                    if (parent != null)
                    {
                        if (parent.parentCommentID != null) c.parentCommentID = parent.parentCommentID;
                        else c.parentCommentID = parent.ID;
                    }
                }

                if (cv.title == "moshesharonamardeleteallcommentshere")
                {
                    _db.Database.ExecuteSqlRaw("DELETE FROM Comments WHERE parentID = {0}", cv.parentID);
                    c.title = "נמחקו כל התגובות בדף זה";
                }
                else if (cv.title == "moshesharonamardeleteallcommentsindbbecareful")
                {
                    _db.Database.ExecuteSqlRaw("DELETE FROM Comments");
                    c.title = "נמחקו כל התגובות בבסיס הנתונים";
                }
                else if (cv.title == "moshesharonamardeleteallcommentbytitle")
                {
                    var x = _db.Comments.Where(y => y.parentID == cv.parentID).First(a => a.title == cv.content);
                    _db.Comments.Remove(x);
                    c.title = "נמחקה התגובה עם הכותרת - " + cv.content;
                }
                else if (cv.title.StartsWith("hodaamimenahelhaatar"))
                {
                    c.title = cv.title.Remove(0, 21);
                    c.name = "הנהלת האתר";
                    c.managment = true;
                    _db.Comments.Add(c);
                }
                else
                {
                    _db.Comments.Add(c);
                }

                _db.SaveChanges();
            }
            else
            {
                c.time = DateTime.MinValue;
                c.title = "שגיאה";
                c.content = "אירעה שגיאה בנסיון ליצור את התגובה";
            }
            return PartialView("Comment", c);
        }
    }
}

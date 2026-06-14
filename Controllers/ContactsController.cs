using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Mvc;
using MyProject12.Models;
using MyProject12.ViewModels;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Website.Controllers;

namespace MyProject12.Controllers
{

    public class ContactsController : SurfaceController
    {
        private readonly ICaptchaValidator _captchaValidator;

        private readonly DB _db; // Assuming you have a DB context for Entity Framework Core

        public ContactsController(IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider, DB db,
    ICaptchaValidator captchaValidator)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)

        {
            _db = db;
            _captchaValidator = captchaValidator;
        }

        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        [HttpPost]
        public async Task<IActionResult> Create(ContactView cv, string captcha)
        {

            // Validate the captcha first
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha))
            {
                ModelState.AddModelError("captcha", "Captcha validation failed");
            }
            Contact c = new Contact();
            bool succeed = true;
            try
            {
                if (ModelState.IsValid)
                {
                    c.submitTime = DateTime.Now;
                    c.name = cv.name;
                    c.familyName = cv.familyName;
                    c.email = cv.email;
                    c.phone = cv.phone;
                    c.content = cv.content;
                    _db.Contacts.Add(c);
                    _db.SaveChanges();
                }
                else
                {
                    succeed = false;
                }
            }
            catch (Exception ex)
            {
                succeed = false;
            }
            return PartialView("ContactReply",succeed);


        }
    }
}

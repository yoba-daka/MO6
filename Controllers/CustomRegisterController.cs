using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MO6;
using MO6.ViewModels;
using MosheSharon;
using MyProject12.Models;
using MyProject12.Services;
using StackExchange.Profiling.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Cms.Web.Website.Models;

namespace MyProject12.Controllers
{



    public class CustomRegisterController : SurfaceController
    {
        private readonly IMemberManager _memberManager;
        private readonly IMemberService _memberService;
        private readonly IMemberSignInManager _memberSignInManager;
        private readonly ICoreScopeProvider _coreScopeProvider;
        private readonly ICaptchaValidator _captchaValidator;
        private readonly DB _db;
        private readonly EmailService _emailService;
        private readonly IPublishedContentQuery _publishedContetnQuery;
        private readonly IConfiguration _configuration;

        private readonly dynamic account;
        private readonly dynamic special;


        public CustomRegisterController(
            IPublishedContentQuery publishedContetnQuery,
                IMemberManager memberManager,
                IMemberService memberService,
                IUmbracoContextAccessor umbracoContextAccessor,
                IUmbracoDatabaseFactory databaseFactory,
                ServiceContext services,
                AppCaches appCaches,
                IProfilingLogger profilingLogger,
                IPublishedUrlProvider publishedUrlProvider,
                IMemberSignInManager memberSignInManager,
                ICoreScopeProvider coreScopeProvider, ICaptchaValidator captchaValidator, DB db, EmailService emailService, IConfiguration configuration)
                : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _memberManager = memberManager;
            _memberService = memberService;
            _memberSignInManager = memberSignInManager;
            _coreScopeProvider = coreScopeProvider;
            _captchaValidator = captchaValidator;
            _db = db;
            _emailService = emailService;
            _publishedContetnQuery = publishedContetnQuery;
            account = publishedContetnQuery.Content(3738);
            special = publishedContetnQuery.Content(4554);
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> HandleRegisterMember([Bind(Prefix = "registerModel")] RegisterModel model, string captcha, bool takanon)
        {


                if(!account.RegistrationAllowed)
                {
                    base.ModelState.AddModelError("registerModel", "ההרשמה לאתר סגורה כעת");
                }
            bool checkCapthcha = CaptchaSettings.IsEnabled(_configuration, "OnRegister");
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha) && checkCapthcha)
            {
                base.ModelState.AddModelError("registerModel", "אירעה שגיאה, נא לרענן את הדף ולנסות שנית");
            }

            if (!takanon)
            {
                base.ModelState.AddModelError("registerModel", "נדרשת קריאה והסכמה לתקנון");
            }

            if (ModelState.IsValid == false)
            {
                return CurrentUmbracoPage();
            }

            MergeRouteValuesToModel(model);

            IdentityResult result = await RegisterMemberAsync(model,false);
            if (result.Succeeded)
            {
                try
                {

                    //EmailVerification ev = new EmailVerification { Code = Helpers.GenerateUniqueCode(5), Created = DateTime.Now, Email = model.Email, TimesSent = 0 };
                    //_db.EmailVerifications.Add(ev);
                    //_db.SaveChanges();

                    //var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });
                    //await _emailService.SendVerificationEmail(model.Name, ev.Email, ev.Code, link);
                    await _emailService.GenerateSendVerificationEmail(model.Name, model.Email, Url);


                    // Notify management
                    try
                    {
                        foreach (string email in ((string)account.NewMemberEmailToManagement).Split(','))
                        {
                            await _emailService.SendManagementNewMemberEmail(model.Name, model.Email, email);
                        }
                    }
                    catch { /* Fail silently if notification fails */ }

                    TempData["FormSuccess"] = true;
                    if (model.RedirectUrl.IsNullOrWhiteSpace() == false)
                    {
                        return Redirect(model.RedirectUrl!);
                    }

                }
                catch
                {
                    TempData["FormFailure"] = true;
                }

                //return RedirectToCurrentUmbracoPage(); //has problem with non ascii characters

                return new CustomRedirectResult();
            }
            AddErrors(result);
            return CurrentUmbracoPage();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> HandleExistingMemberSpecialOffer([Bind(Prefix = "specialOfferLoginModel")] SpecialOfferLoginViewModel model, string captcha)
        {
            ViewData["LoginFormUsed"] = true;

            // Check if the special offer is active
            if (special == null || !special.IsOn)
            {
                ModelState.AddModelError("specialOfferLoginModel", "המבצע הסתיים ואינו זמין יותר.");
                return CurrentUmbracoPage();
            }

            // Validate reCAPTCHA
            bool checkCapthcha = CaptchaSettings.IsEnabled(_configuration, "OnRegister");
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha) && checkCapthcha)
            {
                ModelState.AddModelError("specialOfferLoginModel", "אימות האבטחה נכשל. אנא רענן את הדף ונסה שנית.");
            }

            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Find the member by email
            var identityUser = await _memberManager.FindByEmailAsync(model.Email);
            if (identityUser == null)
            {
                ModelState.AddModelError("specialOfferLoginModel", "האימייל או הסיסמא אינם נכונים");
                return CurrentUmbracoPage();
            }

            // Use IMemberManager.CheckPasswordAsync to verify the password. It returns a bool.
            bool isPasswordCorrect = await _memberManager.CheckPasswordAsync(identityUser, model.Password);

            if (isPasswordCorrect)
            {
                // Check if the member already has a membership record in your custom table
                bool hasMembership = _db.Memberships.Any(m => m.memberID == identityUser.Id.ToString());

                if (!hasMembership)
                {
                    // If they don't have a membership, create a new one for 1 month
                    var membership = new Membership
                    {
                        memberID = identityUser.Id.ToString(),
                        expiration = DateTime.Now.AddMonths(1),
                        isMonthly = false,
                        isMonthlyActive = false,
                        transactions = "חודש-מתנה;",
                        phone = "" // Or fetch from member properties if available
                    };

                    _db.Memberships.Add(membership);
                    await _db.SaveChangesAsync();

                    // Notify management about the existing member claiming the offer
                    try
                    {
                        foreach (string email in ((string)account.NewMemberEmailToManagement).Split(','))
                        {
                            await _emailService.SendManagementNewMemberEmail(identityUser.Name, identityUser.Email, email, new string[] { "מימוש הטבה - חודש חינם (חבר קיים)" });
                        }
                    }
                    catch { /* Fail silently if notification fails */ }

                    TempData["ExistingMemberSuccess"] = true;
                    return CurrentUmbracoPage();
                }
                else
                {
                    // The member already has a membership record, so they are not eligible
                    ModelState.AddModelError("specialOfferLoginModel", "חשבונך אינו זכאי להטבה זו (כבר יש לך מנוי או שניצלת הטבה בעבר).");
                }
            }
            else
            {
                // Password check failed
                ModelState.AddModelError("specialOfferLoginModel", "האימייל או הסיסמא אינם נכונים");
            }

            return CurrentUmbracoPage();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> HandleSpecialOfferRegister([Bind(Prefix = "specialOfferModel")] MO6.ViewModels.SpecialOfferRegisterViewModel model, string captcha)
        {
            // Use the 'special' field to check if the offer is active
            if (special == null || !special.IsOn)
            {
                ModelState.AddModelError("specialOfferModel", "המבצע הסתיים ואינו זמין יותר.");
                return CurrentUmbracoPage();
            }

            bool checkCapthcha = CaptchaSettings.IsEnabled(_configuration, "OnRegister");
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha) && checkCapthcha)
            {
                ModelState.AddModelError("specialOfferModel", "אימות האבטחה נכשל. אנא רענן את הדף ונסה שנית.");
            }

            if (!bool.TryParse(Request.Form["takanon"], out var takanon) || !takanon)
            {
                ModelState.AddModelError("specialOfferModel", "נדרשת קריאה והסכמה לתקנון");
            }

            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Check if member already exists before trying to create
            var existingMember = await _memberManager.FindByEmailAsync(model.Email);
            if (existingMember != null)
            {
                ModelState.AddModelError("specialOfferModel", "כתובת האימייל הזו כבר קיימת במערכת");
                return CurrentUmbracoPage();
            }

            var registerModel = new RegisterModel
            {
                Name = model.Name,
                Email = model.Email,
                Password = model.Password,
                Username = model.Email,
                UsernameIsEmail = true,
                MemberTypeAlias = Constants.Conventions.MemberTypes.DefaultAlias
            };

            // Use the existing, working method to create the Umbraco member
            IdentityResult result = await RegisterMemberAsync(registerModel, false); // false = don't log them in yet

            if (result.Succeeded)
            {
                try
                {
                    // Re-fetch the newly created member to get the correct object and Id
                    var newMemberIdentityUser = await _memberManager.FindByEmailAsync(model.Email);
                    if (newMemberIdentityUser != null)
                    {
                        // Create the free 1-Month Membership
                        var membership = new Membership
                        {
                            memberID = newMemberIdentityUser.Id.ToString(),
                            expiration = DateTime.Now.AddMonths(1),
                            isMonthly = false,
                            isMonthlyActive = false,
                            transactions = "חודש-מתנה;",
                            phone = ""
                        };

                        _db.Memberships.Add(membership);
                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not find the newly created member with email: {model.Email}");
                    }

                    // Send verification email
                    //EmailVerification ev = new EmailVerification { Code = Helpers.GenerateUniqueCode(5), Created = DateTime.Now, Email = model.Email, TimesSent = 0 };
                    //_db.EmailVerifications.Add(ev);
                    //await _db.SaveChangesAsync();

                    //var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });
                    //await _emailService.SendVerificationEmail(model.Name, model.Email, ev.Code, link);
                    await _emailService.GenerateSendVerificationEmail(model.Name, model.Email, Url);


                    // Notify management
                    try
                    {
                        foreach (string email in ((string)account.NewMemberEmailToManagement).Split(','))
                        {
                            await _emailService.SendManagementNewMemberEmail(model.Name, model.Email, email,new string[] {"כולל מנוי חודש חינם"});
                        }
                    }
                    catch { /* Fail silently if notification fails */ }

                    TempData["FormSuccess"] = true;
                    return CurrentUmbracoPage();
                }
                catch (Exception ex)
                {
                    // Log the exception ex...
                    TempData["FormFailure"] = true;

                    // Clean up the created member if the subsequent steps fail
                    var memberToDelete = await _memberManager.FindByEmailAsync(model.Email);
                    if (memberToDelete != null) await _memberManager.DeleteAsync(memberToDelete);

                    ModelState.AddModelError("specialOfferModel", "אירעה שגיאה בלתי צפויה בתהליך, אנא נסו שוב.");
                    return CurrentUmbracoPage();
                }
            }

            // Add ASP.NET Identity errors to ModelState if registration fails
            AddErrors(result);
            return CurrentUmbracoPage();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        private void MergeRouteValuesToModel(RegisterModel model)
        {
            if (RouteData.Values.TryGetValue(nameof(RegisterModel.RedirectUrl), out var redirectUrl) && redirectUrl != null)
            {
                model.RedirectUrl = redirectUrl.ToString();
            }

            if (RouteData.Values.TryGetValue(nameof(RegisterModel.MemberTypeAlias), out var memberTypeAlias) &&
                memberTypeAlias != null)
            {
                model.MemberTypeAlias = memberTypeAlias.ToString()!;
            }

            if (RouteData.Values.TryGetValue(nameof(RegisterModel.UsernameIsEmail), out var usernameIsEmail) &&
                usernameIsEmail != null)
            {
                model.UsernameIsEmail = usernameIsEmail.ToString() == "True";
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (IdentityError? error in result.Errors)
            {
                if (error.Code == "DuplicateUserName") continue;
                if(error.Code == "DuplicateEmail")
                {
                    error.Description = "כתובת האימייל הזו כבר בשימוש על ידי משתמש אחר";
                }
                ModelState.AddModelError("registerModel", error.Description);
            }
        }

        //Here we created a helper Method to assign a MemberGroup to a member.
        private void AssignMemberGroup(string email, string group)
        {
            try
            {
                _memberService.AssignRole(email, group);
            }
            catch (Exception ex)
            {
                //handle the exception
            }

        }


        /// <summary>

        /// </summary>
        /// <param name="model">Register member model.</param>
        /// <param name="logMemberIn">Flag for whether to log the member in upon successful registration.</param>
        /// <returns>Result of registration operation.</returns>
        private async Task<IdentityResult> RegisterMemberAsync(RegisterModel model, bool logMemberIn = true)
        {
            using ICoreScope scope = _coreScopeProvider.CreateCoreScope(autoComplete: true);


            if (string.IsNullOrEmpty(model.Name) && string.IsNullOrEmpty(model.Email) == false)
            {
                model.Name = model.Email;
            }

            model.Username = model.UsernameIsEmail || model.Username == null ? model.Email : model.Username;

            var identityUser =
                MemberIdentityUser.CreateNew(model.Username, model.Email, model.MemberTypeAlias, false, model.Name);
            IdentityResult identityResult = await _memberManager.CreateAsync(
                identityUser,
                model.Password);

            if (identityResult.Succeeded)
            {

                IMember? member = _memberService.GetByKey(identityUser.Key);
                if (member == null)
                {

                    throw new InvalidOperationException($"Could not find a member with key: {member?.Key}.");
                }

                foreach (MemberPropertyModel property in model.MemberProperties.Where(p => p.Value != null).Where(property => member.Properties.Contains(property.Alias)))
                {
                    member.Properties[property.Alias]?.SetValue(property.Value);
                }

                //Before we save the member we make sure to assign the group, for this the "Group" must exist in the backoffice.
                //string memberGroup = "professionals";
                //AssignMemberGroup(model.Email, memberGroup);

                _memberService.Save(member);

                if (logMemberIn)
                {
                    await _memberSignInManager.SignInAsync(identityUser, false);
                }
            }

            return identityResult;
        }
    }
}

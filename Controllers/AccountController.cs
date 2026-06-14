using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MosheSharon;
using MyProject12.Models;
using MyProject12.Services;
using MyProject12.ViewModels;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
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
using static Umbraco.Cms.Core.Constants.Conventions;

namespace MyProject12.Controllers
{
    public class AccountController : SurfaceController
    {

        private readonly IMemberManager _memberManager;

        private readonly IMemberSignInManager _signInManager;

        private readonly ITwoFactorLoginService _twoFactorLoginService;

        private readonly ICaptchaValidator _captchaValidator;

        private readonly DB _db;

        private readonly EmailService _emailService;

        private readonly MeshulamService _meshulaService;


        [ActivatorUtilitiesConstructor]
        public AccountController(IUmbracoContextAccessor umbracoContextAccessor, IUmbracoDatabaseFactory databaseFactory, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, DB db, EmailService emailService,MeshulamService meshulamService, IPublishedUrlProvider publishedUrlProvider, IMemberSignInManager signInManager, IMemberManager memberManager, ITwoFactorLoginService twoFactorLoginService, ICaptchaValidator captchaValidator)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _signInManager = signInManager;
            _memberManager = memberManager;
            _twoFactorLoginService = twoFactorLoginService;
            _captchaValidator = captchaValidator;
            _db = db;
            _emailService = emailService;
            _meshulaService = meshulamService;
        }
        // Action for handling full name change
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]

        public async Task<IActionResult> HandleFullNameChange(FullNameChangeViewModel model)
        {
            TempData["OpenModal"] = "changeFullNameModal";

            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Get the currently logged-in member
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                ModelState.AddModelError("", "משתמש לא מחובר.");
                return CurrentUmbracoPage();
            }

            // Verify current password
            if (!await _memberManager.CheckPasswordAsync(member, model.CurrentPassword))
            {
                ModelState.AddModelError("CurrentPassword", "הסיסמה אינה נכונה.");
                return CurrentUmbracoPage();
            }

            // Check for existing NameChange records
            var nameChangeRecord = _db.NameChanges.FirstOrDefault(nc => nc.memberId == member.Id);

            // If no record exists, create one
            if (nameChangeRecord == null)
            {
                nameChangeRecord = new NameChange
                {
                    memberId = member.Id,
                    TimesSent = 1,
                    Created = DateTime.UtcNow
                };
                _db.NameChanges.Add(nameChangeRecord);
            }
            else if (nameChangeRecord.TimesSent >= 3)
            {
                ModelState.AddModelError("FullNameChange", "לא ניתן לשנות את השם יותר משלוש פעמים - צרו קשר.");
                return CurrentUmbracoPage();
            }
            else
            {
                nameChangeRecord.TimesSent++;
            }

            // Save changes to the database
            await _db.SaveChangesAsync();

            // Update the full name
            member.Name = model.FullName;
            await _memberManager.UpdateAsync(member);

            TempData["SuccessMessageName"] = "השם שונה בהצלחה!";
            return CurrentUmbracoPage();
        }

        // Action for handling email change
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]

        public async Task<IActionResult> HandleEmailChange(EmailChangeViewModel model)
        {
            TempData["OpenModal"] = "changeEmailModal";

            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Get the currently logged-in member
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                // Handle not logged in scenario
                ModelState.AddModelError("EmailChange", "משתמש לא מחובר.");
                return CurrentUmbracoPage();
            }

            // Verify current password
            if (!await _memberManager.CheckPasswordAsync(member, model.CurrentPassword))
            {
                ModelState.AddModelError("CurrentPassword", "הסיסמה אינה נכונה.");
                return CurrentUmbracoPage();
            }

            // Check if new email is different from the current one
            if (member.Email == model.Email)
            {
                ModelState.AddModelError("Email", "כתובת האימייל החדשה זהה לכתובת הנוכחית.");
                return CurrentUmbracoPage();
            }


            try
            {
                var evExists = _db.EmailVerifications.FirstOrDefault(x => x.Email.EndsWith(";"+member.Email));
                if (evExists != null)
                {
                    _db.Remove(evExists);
                    _db.SaveChanges();
                }

                //Sends verification email
                EmailVerification ev = new EmailVerification { Code = Helpers.GenerateUniqueCode(5), Created = DateTime.Now, Email = model.Email + ";" + member.Email, TimesSent = 0 };
                _db.EmailVerifications.Add(ev);
                _db.SaveChanges();
                var link = Url.SurfaceAction("VerifyEmailChange", "Account",
                              new { email = model.Email, code = ev.Code });
                await _emailService.SendChangeEmailEmail(member.Name, model.Email, ev.Code, link);
            }
            catch
            {
                ModelState.AddModelError("EmailChange", "אירעה שגיאה בנסיון שליחת אימייל האימות");
            }




            TempData["SuccessMessageEmail"] = "לינק לאימות כתובת האימייל נשלח לכתובת החדשה!";
            return CurrentUmbracoPage();
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[ValidateUmbracoFormRouteString]
        //public async Task<IActionResult> sendVerificationEmail(string newEmail)
        //{
        //    try
        //    {
        //        var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email.StartsWith(newEmail + ";"));
        //        var emails = ev.Email.Split(';');
        //        var member = await _memberManager.FindByEmailAsync(emails[1]);
        //        if (ev != null && ev.TimesSent < 3)
        //        {
        //            ev.TimesSent++;
        //            _db.EmailVerifications.Update(ev);
        //            _db.SaveChanges();
        //            var link = Url.SurfaceAction("VerifyEmailChange", "Account",
        //                          new { email = newEmail, code = ev.Code });
        //            await _emailService.SendChangeEmailEmail(member.Name, newEmail, ev.Code, link);
        //        }
        //        else
        //        {
        //            return StatusCode(500, "לא ניתן לשלוח יותר משלושה אימיילים");
        //        }
        //    }
        //    catch { return StatusCode(500, "אירעה שגיאה"); }
        //    return Ok();
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> sendVerificationEmail(string newEmail)
        {
            try
            {
                // First, get the currently logged-in member to securely access their existing email.
                var member = await _memberManager.GetCurrentMemberAsync();
                if (member == null)
                {
                    // This should not happen if the user is on their account page, but it's a good safeguard.
                    return StatusCode(401, "משתמש לא מחובר.");
                }
                var oldEmail = member.Email;

                // Construct the unique composite email string used for this verification type.
                var compositeEmail = $"{newEmail};{oldEmail}";

                var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email == compositeEmail);

                if (ev != null)
                {
                    // Logic for an existing record: check the resend limit.
                    if (ev.TimesSent >= 3)
                    {
                        // Now this error message is returned in the correct context.
                        return StatusCode(429, "לא ניתן לשלוח יותר משלושה אימיילים לאימות כתובת זו.");
                    }
                    ev.TimesSent++;
                    _db.EmailVerifications.Update(ev);
                }
                else
                {
                    // THIS IS THE FIX: If no record exists for this specific email change, create one.
                    ev = new EmailVerification
                    {
                        Code = Helpers.GenerateUniqueCode(5),
                        Created = DateTime.Now,
                        Email = compositeEmail, // Store the composite email string
                        TimesSent = 1           // This is the first time sending.
                    };
                    _db.EmailVerifications.Add(ev);
                }

                await _db.SaveChangesAsync();

                // This code now runs for both existing and newly created records.
                var link = Url.SurfaceAction("VerifyEmailChange", "Account", new { email = newEmail, code = ev.Code });
                await _emailService.SendChangeEmailEmail(member.Name, newEmail, ev.Code, link);

                return Ok();
            }
            catch
            {
                return StatusCode(500, "אירעה שגיאה");
            }
        }


        [HttpGet]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> VerifyEmailChange(string email, string code)
        {
            try
            {
                bool verified = await verifyMember(email, code);
                base.TempData["verifyNewEmail"] = verified;
            }
            catch (Exception ex) { }
            return Redirect("/");
        }

        private async Task<bool> verifyMember(string email, string code)
        {

            var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email.StartsWith(email+";"));
            if (ev != null && ev.Code == code)
            {
                var emails = ev.Email.Split(';');
                var member = await _memberManager.FindByEmailAsync(emails[1]);
                if (member != null)
                {
                    member.IsApproved = true;
                    member.Email = email;
                    member.UserName = email;

                    var membership = _db.Memberships.FirstOrDefault(x=>x.memberID == member.Id);
                    if (membership != null && membership.isMonthlyActive)
                    {
                        var transacrtion = _db.Transactions.OrderByDescending(x => x.Created).FirstOrDefault(x => x.PayerEmail == member.Email);
                        if (transacrtion != null)
                        {
                            var success = await _meshulaService.UpdateDirectDebit(transacrtion, false, email);
                            if (!success)
                            {
                                return false;
                            }
                        }
                    }
                    await _memberManager.UpdateAsync(member);
                    //await _signInManager.SignInAsync(member, isPersistent: true);
                    _db.Remove(ev);
                    _db.SaveChanges();

                    return true;

                }
            }
            return false;
        }

        // Action for handling password change
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> HandlePasswordChange(PasswordChangeViewModel model)
        {
            TempData["OpenModal"] = "changePasswordModal";

            if (!ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            // Get the currently logged-in member
            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                // Handle not logged in scenario
                ModelState.AddModelError("PasswordChange", "משתמש לא מחובר.");
                return CurrentUmbracoPage();
            }

            // Verify current password
            if (!await _memberManager.CheckPasswordAsync(member, model.CurrentPassword))
            {
                ModelState.AddModelError("CurrentPassword", "הסיסמה שהוזנה שגויה.");
                return CurrentUmbracoPage();
            }

            // Update the password
            var result = await _memberManager.ChangePasswordAsync(member,model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                // Handle the case where the password change failed
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("PasswordChange", error.Description);
                }
                return CurrentUmbracoPage();
            }

            TempData["SuccessMessagePassword"] = "הסיסמא שונתה בהצלחה!";
            return CurrentUmbracoPage();
        }
    }
}

using Amazon.SimpleEmail.Model.Internal.MarshallTransformations;
using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MO6;
using MosheSharon;
using MyProject12.Models;
using MyProject12.Services;
using Org.BouncyCastle.Crypto;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.ActionsResults;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Common.Models;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;

namespace MyProject12.Controllers
{
    public class CustomLoginController : SurfaceController
    {

        private readonly IMemberManager _memberManager;

        private readonly IMemberSignInManager _signInManager;

        private readonly ITwoFactorLoginService _twoFactorLoginService;

        private readonly ICaptchaValidator _captchaValidator;

        private readonly DB _db;

        private readonly EmailService _emailService;

        private readonly MeshulamService _meshulaService;

        private readonly IConfiguration _configuration;



        [ActivatorUtilitiesConstructor]
        public CustomLoginController(IUmbracoContextAccessor umbracoContextAccessor, IUmbracoDatabaseFactory databaseFactory, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, DB db, EmailService emailService, IPublishedUrlProvider publishedUrlProvider, IMemberSignInManager signInManager, IMemberManager memberManager, ITwoFactorLoginService twoFactorLoginService, ICaptchaValidator captchaValidator, MeshulamService meshulamService, IConfiguration configuration)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _signInManager = signInManager;
            _memberManager = memberManager;
            _twoFactorLoginService = twoFactorLoginService;
            _captchaValidator = captchaValidator;
            _db = db;
            _emailService = emailService;
            _meshulaService = meshulamService;
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> HandleLogin([Bind(new string[] { }, Prefix = "loginModel")] LoginModel model, string captcha, string verificationCode)
        {
            if (!string.IsNullOrEmpty(verificationCode))
            {
                await verifyMember(model.Username, verificationCode);
            }
            else ModelState.Remove("verificationCode");

            bool checkCapthcha = CaptchaSettings.IsEnabled(_configuration, "OnLogin");
            if (!await _captchaValidator.IsCaptchaPassedAsync(captcha) && checkCapthcha)
            {
                base.ModelState.AddModelError("loginModel", "אירעה שגיאה, נא לרענן את הדף ולנסות שנית") ;
            }
            if (!base.ModelState.IsValid)
            {
                return CurrentUmbracoPage();
            }

            MergeRouteValuesToModel(model);
            DateTime? lastLogin = (await _memberManager.FindByNameAsync(model.Username))?.LastLoginDateUtc;
            Microsoft.AspNetCore.Identity.SignInResult signInResult = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (signInResult.Succeeded)
            {
                base.TempData["LoginSuccess"] = true;
                if(lastLogin == null)
                {
                    return new CustomRedirectResult("/חשבון");

                }

                if (!model.RedirectUrl.IsNullOrWhiteSpace())
                {
                    return Redirect(base.Url.IsLocalUrl(model.RedirectUrl) ? model.RedirectUrl : CurrentPage.AncestorOrSelf(1).Url(base.PublishedUrlProvider));
                }

                return RedirectToCurrentUmbracoUrl();
            }

            if (signInResult.RequiresTwoFactor)
            {
                MemberIdentityUser memberIdentityUser = await _memberManager.FindByNameAsync(model.Username);
                if (memberIdentityUser == null)
                {
                    return new ValidationErrorResult("No local member found for username " + model.Username);
                }

                IEnumerable<string> providerNames = await _twoFactorLoginService.GetEnabledTwoFactorProviderNamesAsync(memberIdentityUser.Key);
                base.ViewData.SetTwoFactorProviderNames(providerNames);
            }
            else if (signInResult.IsLockedOut)
            {
                base.ModelState.AddModelError("loginModel", "המשתמש נעול - יש לאפס סיסמא");
            }
            else if (signInResult.IsNotAllowed)
            {
                base.TempData["emailForVerification"] = model.Username;
                base.ModelState.AddModelError("loginModel", "נדרש אימות לחשבון");
            }
            else
            {
                base.ModelState.AddModelError("loginModel", "האימייל או הסיסמא אינם נכונים");
            }



            return CurrentUmbracoPage();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> CancelKeva(bool cancel)
        {

            if (!cancel)
            {
                return StatusCode(500, "אירעה שגיאה");
            }

            var member = await _memberManager.GetCurrentMemberAsync();
            if (member == null)
            {
                return StatusCode(401, "משתמש לא מחובר.");
            }

            var membership = _db.Memberships.GetPreferredMonthlyMembership(member.Id);
            if (membership == null)
            {
                return StatusCode(404, "מנוי לא נמצא.");
            }

            if (!membership.isMonthly)
            {
                return StatusCode(400, "המנוי אינו בהוראת קבע.");
            }

            if (!membership.isMonthlyActive)
            {
                return Ok();
            }

            var parsedRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
            var parsedTransactionIds = MembershipCancellationHelper.ParseTransactionIds(parsedRefs);
            var parsedTransactionTokens = MembershipCancellationHelper.ParseTransactionTokens(parsedRefs);
            var normalizedEmail = MembershipCancellationHelper.NormalizeComparisonValue(member.Email);

            var candidateTransactions = _db.Transactions
                .Where(x =>
                    (x.TransactionId.HasValue && parsedTransactionIds.Contains(x.TransactionId.Value)) ||
                    (!string.IsNullOrWhiteSpace(x.TransactionToken) &&
                     parsedTransactionTokens.Contains(x.TransactionToken.Trim().ToLower())) ||
                    (!string.IsNullOrWhiteSpace(x.PayerEmail) &&
                     x.PayerEmail.Trim().ToLower() == normalizedEmail))
                .OrderByDescending(x => x.Created)
                .Take(250)
                .ToList();

            var candidateDirectDebitIds = candidateTransactions
                .Where(x => x.DirectDebitId.HasValue && x.DirectDebitId.Value > 0)
                .Select(x => x.DirectDebitId!.Value)
                .Distinct()
                .ToList();

            if (candidateDirectDebitIds.Count > 0)
            {
                var relatedDirectDebitTransactions = _db.Transactions
                    .Where(x => x.DirectDebitId.HasValue && candidateDirectDebitIds.Contains(x.DirectDebitId.Value))
                    .OrderByDescending(x => x.Created)
                    .Take(250)
                    .ToList();

                candidateTransactions = candidateTransactions
                    .Concat(relatedDirectDebitTransactions)
                    .GroupBy(x => x.ID)
                    .Select(x => x.First())
                    .ToList();
            }

            var transaction = MembershipCancellationHelper.SelectProviderCancellationCandidate(
                candidateTransactions,
                parsedTransactionIds,
                parsedTransactionTokens,
                member.Email,
                candidateDirectDebitIds);

            if (transaction == null)
            {
                return StatusCode(500, "לא נמצאה עסקה מתאימה עם מזהי ספק תקינים לביטול הוראת הקבע.");
            }

            if (!await _meshulaService.CancelDirectDebit(transaction, member.Email))
            {
                return StatusCode(500, "אירעה שגיאה בביטול הוראת הקבע.");
            }

            membership.isMonthlyActive = false;
            _db.Memberships.Update(membership);
            _db.SaveChanges();

            return Ok();
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[ValidateUmbracoFormRouteString]
        //public async Task<IActionResult> sendVerificationEmail(string email)
        //{
        //    try
        //    {
        //        var member = await _memberManager.FindByEmailAsync(email);
        //        var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email == email);
        //        if (ev != null && ev.TimesSent < 3)
        //        {
        //            ev.TimesSent++;
        //            _db.EmailVerifications.Update(ev);
        //            _db.SaveChanges();
        //            var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });


        //            await _emailService.SendVerificationEmail(member.Name, ev.Email, ev.Code, link);
        //        }
        //    }
        //    catch { return StatusCode(500, "אירעה שגיאה"); }
        //    return Ok();
        //}


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> sendVerificationEmail(string email)
        {
            try
            {
                var member = await _memberManager.FindByEmailAsync(email);
                // We need a member to send an email to.
                if (member == null)
                {
                    // Fail silently to prevent user enumeration
                    return Ok();
                }

                var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email == email);

                if (ev != null)
                {
                    // Logic for an existing record: check the resend limit
                    if (ev.TimesSent >= 3)
                    {
                        // Optionally, you could return a message here, but for now, we'll just stop.
                        return Ok();
                    }
                    ev.TimesSent++;
                    _db.EmailVerifications.Update(ev);
                }
                else
                {
                    // THIS IS THE FIX: If no record exists, create one.
                    ev = new EmailVerification
                    {
                        Code = Helpers.GenerateUniqueCode(5),
                        Created = DateTime.Now,
                        Email = email,
                        TimesSent = 1 // This is the first time sending (or re-sending)
                    };
                    _db.EmailVerifications.Add(ev);
                }

                await _db.SaveChangesAsync();

                // This code now runs for both existing and new records
                var link = Url.RouteUrl("EmailVerification", new { email = ev.Email, code = ev.Code });
                await _emailService.SendVerificationEmail(member.Name, ev.Email, ev.Code, link);

                return Ok();
            }
            catch
            {
                return StatusCode(500, "אירעה שגיאה");
            }
        }

        [Route("verification", Name = "EmailVerification")]
        [HttpGet]
        public async Task<IActionResult> VerificationLogin(string email, string code)
        {
            bool verified = await verifyMember(email, code);
            base.TempData["verify"] = verified;
            return Redirect("/");
        }

        private async Task<bool> verifyMember(string email, string code)
        {

            var ev = _db.EmailVerifications.FirstOrDefault(x => x.Email == email);
            if (ev != null && ev.Code == code)
            {
                var member = await _memberManager.FindByEmailAsync(email);
                if (member != null)
                {
                    member.IsApproved = true;
                    await _memberManager.UpdateAsync(member);
                    //await _signInManager.SignInAsync(member, isPersistent: true);
                    await _db.EmailVerifications.Where(x => x.Email == email).ExecuteDeleteAsync();
                    return true;

                }
            }
            return false;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]
        public async Task<IActionResult> SendPasswordReset(string email)
        {
            try
            {
                var member = await _memberManager.FindByEmailAsync(email);
                if (member == null)
                {
                    return Ok();
                }

                var pr = _db.PasswordResets.FirstOrDefault(x => x.Email == email);
                if (pr == null)
                {
                    pr = new PasswordReset
                    {
                        Created = DateTime.UtcNow,
                        Email = email,
                        TimesSent = 1,
                        Token = generatePasswordToken()
                    };
                    _db.PasswordResets.Add(pr);
                }
                else
                {
                    // Check if the limit of password reset attempts has been reached
                    const int maxAttempts = 3; // For example, limit to 3 attempts
                    if (pr.TimesSent >= maxAttempts)
                    {
                        return BadRequest("הגעתם למגבלת הנסיונות לאיפוס הסיסמה. אנא צרו קשר.");
                    }

                    pr.TimesSent++;
                    pr.Token = generatePasswordToken(); // Regenerate token for security reasons
                    pr.Created = DateTime.UtcNow;       // Update creation time
                }

                await _db.SaveChangesAsync();

                //var resetLink = Url.SurfaceAction("PasswordReset", "CustomLogin", new { email = email, token = pr.Token });
                var resetLink = Url.RouteUrl("PasswordReset", new { email = email, token = pr.Token });



                // Send the password reset email
                await _emailService.SendPasswordResetEmail(member.Name, email, resetLink);

                return Ok("נשלח מייל עם קישור לאיפוס סיסמא" );
            }
            catch (Exception ex)
            {
                // Log the exception details for debugging
                // Consider using a logging framework
                return StatusCode(500, "אירעה שגיאה");
            }
        }
        //[HttpGet]
        //public async Task<IActionResult> PasswordReset(string email, string token)
        //{
        //    bool verified = await verifyMember(email, token);
        //    base.TempData["email"] = email;
        //    base.TempData["token"] = token;
        //    return Redirect("/");
        //}

        private string generatePasswordToken(int size = 32)
        {
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                var tokenBytes = new byte[size];
                randomNumberGenerator.GetBytes(tokenBytes);

                // Convert the byte array to a Base64 string
                return Convert.ToBase64String(tokenBytes);
            }
        }

        [Route("passwordreset", Name = "PasswordReset")]
        [HttpGet]
        public async Task<IActionResult> PasswordReset(string email, string token)
        {
            var passwordReset = _db.PasswordResets
                .FirstOrDefault(pr => pr.Email == email && pr.Token == token);

            if (passwordReset == null)
            {
                TempData["PasswordMessage"] = "הקישור לאיפוס סיסמה לא תקין או פג תוקף.";
                return Redirect("/");
            }

            if (DateTime.UtcNow - passwordReset.Created > TimeSpan.FromHours(24))
            {
                TempData["PasswordMessage"] = "הקישור לאיפוס סיסמה פג תוקף.";
                return Redirect("/");
            }

            // Token is valid, show reset password form
            TempData["Email"] = email;
            TempData["Token"] = token;
            return Redirect("/");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateUmbracoFormRouteString]

        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                return BadRequest(new { Message = "נתונים חסרים." });
            }

            var member = await _memberManager.FindByEmailAsync(email);
            if (member == null)
            {
                return BadRequest(new { Message = "משתמש לא נמצא." });
            }

            var passwordReset = _db.PasswordResets
                .FirstOrDefault(pr => pr.Email == email && pr.Token == token);

            if (passwordReset == null || DateTime.UtcNow - passwordReset.Created > TimeSpan.FromHours(24))
            {
                return BadRequest(new { Message = "קישור לאיפוס סיסמה לא תקין או פג תוקף." });
            }
            var umbracoToken = await _memberManager.GeneratePasswordResetTokenAsync(member);
            var resetResult = await _memberManager.ResetPasswordAsync(member, umbracoToken, newPassword);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new { Message = "אירעה שגיאה בעדכון הסיסמה." });
            }

            // Check if the member is locked out
            if (await _memberManager.IsLockedOutAsync(member))
            {
                // Unlock the member
                await _memberManager.ResetAccessFailedCountAsync(member);
                await _memberManager.SetLockoutEndDateAsync(member, DateTimeOffset.UtcNow);
            }
            _db.PasswordResets.Remove(passwordReset);
            _db.SaveChanges();

            return Ok(new { Message = "הסיסמה עודכנה בהצלחה." });
        }

        //
        // Summary:
        //     We pass in values via encrypted route values so they cannot be tampered with
        //     and merge them into the model for use
        //
        // Parameters:
        //   model:

        private void MergeRouteValuesToModel(LoginModel model)
        {
            if (base.RouteData.Values.TryGetValue("RedirectUrl", out var value) && value != null)
            {
                model.RedirectUrl = value.ToString();
            }
        }
    }
}

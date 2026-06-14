using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MO6.Models;
using MosheSharon;
using MyProject12.Models;
using System.Globalization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Models;

namespace MyProject12.Services
{
    public class EveryHour : BackgroundService
    {
        private static readonly TimeSpan AutoHealWindow = TimeSpan.FromHours(24);
        private static readonly TimeSpan AutoHealDelay = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EveryHour> _logger;

        public EveryHour(IServiceScopeFactory scopeFactory, ILogger<EveryHour> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DB>();
                    var memberManager = scope.ServiceProvider.GetRequiredService<MemberManager>();
                    var meshulamService = scope.ServiceProvider.GetRequiredService<MeshulamService>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                    var temporaryMemberResolver = scope.ServiceProvider.GetRequiredService<TemporaryMemberResolver>();
                    var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
                    var passwordProtector = dataProtectionProvider.CreateProtector("MO6.TemporaryMembers.Password");

                    await AutoHealSuccessfulTransactionsAsync(
                        db,
                        memberManager,
                        meshulamService,
                        emailService,
                        temporaryMemberResolver,
                        passwordProtector,
                        stoppingToken);

                    await CleanupUnverifiedMembersAsync(db, memberManager, stoppingToken);
                    CleanupNameChanges(db);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Hourly maintenance iteration failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task AutoHealSuccessfulTransactionsAsync(
            DB db,
            MemberManager memberManager,
            MeshulamService meshulamService,
            EmailService emailService,
            TemporaryMemberResolver temporaryMemberResolver,
            IDataProtector passwordProtector,
            CancellationToken stoppingToken)
        {
            var now = DateTime.Now;
            var threshold = now.Subtract(AutoHealWindow);
            var cutoff = now.Subtract(AutoHealDelay);

            var successfulTransactions = await db.Transactions
                .Where(t =>
                    t.Created >= threshold &&
                    t.Created <= cutoff &&
                    (t.StatusCode == 2 || t.Status == "שולם"))
                .OrderBy(t => t.Created)
                .ToListAsync(stoppingToken);

            foreach (var transaction in successfulTransactions)
            {
                stoppingToken.ThrowIfCancellationRequested();

                try
                {
                    if (HasLinkedMembership(db, transaction))
                    {
                        continue;
                    }

                    var resolution = await ResolveMemberForTransactionAsync(
                        db,
                        memberManager,
                        emailService,
                        temporaryMemberResolver,
                        transaction,
                        passwordProtector);

                    if (resolution.Member == null)
                    {
                        _logger.LogWarning("Auto-heal could not resolve member for paid transaction. transactionId={TransactionId}, token={TransactionToken}, email={Email}",
                            transaction.TransactionId,
                            transaction.TransactionToken,
                            transaction.PayerEmail);
                        continue;
                    }

                    bool monthly = meshulamService.IsMonthlyTransaction(transaction);
                    var (_, membershipChanged) = await EnsureMembershipForTransactionAsync(db, resolution.Member, transaction, monthly, stoppingToken);

                    if (resolution.TemporaryMember != null &&
                        !resolution.TemporaryMember.Processed &&
                        (resolution.MemberCreated || membershipChanged))
                    {
                        resolution.TemporaryMember.Processed = true;
                        db.TemporaryMembers.Update(resolution.TemporaryMember);
                    }

                    await db.SaveChangesAsync(stoppingToken);

                    if (resolution.MemberCreated || membershipChanged)
                    {
                        var actionSummary = BuildHealingActionSummary(
                            resolution.MemberCreated,
                            membershipChanged,
                            resolution.VerificationEmailSent,
                            resolution.VerificationEmailError);

                        try
                        {
                            await emailService.SendManagementHealingActionEmail(
                                actionSummary,
                                transaction,
                                monthly,
                                resolution.MemberCreated,
                                membershipChanged,
                                resolution.VerificationEmailSent,
                                resolution.VerificationEmailError,
                                resolution.TemporaryMember);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send auto-heal management email. transactionId={TransactionId}, token={TransactionToken}",
                                transaction.TransactionId,
                                transaction.TransactionToken);
                        }

                        _logger.LogInformation("Auto-heal repaired paid transaction. memberId={MemberId}, email={Email}, token={TransactionToken}, memberCreated={MemberCreated}, membershipChanged={MembershipChanged}",
                            resolution.Member.Id,
                            resolution.Member.Email,
                            transaction.TransactionToken,
                            resolution.MemberCreated,
                            membershipChanged);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-heal failed for paid transaction. transactionId={TransactionId}, token={TransactionToken}",
                        transaction.TransactionId,
                        transaction.TransactionToken);
                }
            }
        }

        private async Task<HealingResolutionResult> ResolveMemberForTransactionAsync(
            DB db,
            MemberManager memberManager,
            EmailService emailService,
            TemporaryMemberResolver temporaryMemberResolver,
            Transaction transaction,
            IDataProtector passwordProtector)
        {
            var resolution = new HealingResolutionResult();

            if (!string.IsNullOrWhiteSpace(transaction.PayerEmail))
            {
                var existingMember = await memberManager.FindByEmailAsync(transaction.PayerEmail);
                if (existingMember != null)
                {
                    resolution.Member = existingMember;
                    return resolution;
                }
            }

            if (transaction.DirectDebitId.HasValue)
            {
                var fallbackTransactions = db.Transactions
                    .Where(x =>
                        x.DirectDebitId == transaction.DirectDebitId &&
                        !string.IsNullOrWhiteSpace(x.PayerEmail) &&
                        x.TransactionToken != transaction.TransactionToken &&
                        (!transaction.TransactionId.HasValue || x.TransactionId != transaction.TransactionId))
                    .OrderByDescending(x => x.Created)
                    .ToList();

                foreach (var previousDirectDebitTransaction in fallbackTransactions)
                {
                    var existingMember = await memberManager.FindByEmailAsync(previousDirectDebitTransaction.PayerEmail);
                    if (existingMember != null)
                    {
                        resolution.Member = existingMember;
                        return resolution;
                    }
                }
            }

            var tempMember = temporaryMemberResolver.ResolveSafeAutoHealCandidate(transaction, AutoHealWindow);
            if (tempMember == null)
            {
                return resolution;
            }

            resolution.TemporaryMember = tempMember;

            var candidate = await memberManager.FindByEmailAsync(tempMember.Email);
            if (candidate != null)
            {
                resolution.Member = candidate;
                return resolution;
            }

            var registerModel = new RegisterModel
            {
                Name = tempMember.Name,
                Email = tempMember.Email,
                Password = GetTemporaryPasswordForRegistration(tempMember.Password, passwordProtector),
                Username = tempMember.Email,
                UsernameIsEmail = true,
                MemberTypeAlias = Constants.Conventions.MemberTypes.DefaultAlias,
                RedirectUrl = null,
                AutomaticLogIn = false
            };

            var identityUser = MemberIdentityUser.CreateNew(
                registerModel.Username,
                registerModel.Email,
                registerModel.MemberTypeAlias,
                false,
                registerModel.Name);

            var identityResult = await memberManager.CreateAsync(identityUser, registerModel.Password);
            if (!identityResult.Succeeded)
            {
                var errors = string.Join(", ", identityResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Auto-heal member creation failed. email={Email}, token={TransactionToken}, errors={Errors}",
                    tempMember.Email,
                    transaction.TransactionToken,
                    errors);
                return resolution;
            }

            resolution.Member = await memberManager.FindByEmailAsync(tempMember.Email);
            resolution.MemberCreated = resolution.Member != null;

            if (resolution.MemberCreated)
            {
                try
                {
                    await emailService.GenerateSendVerificationEmail(tempMember.Name, tempMember.Email);
                    resolution.VerificationEmailSent = true;
                }
                catch (Exception ex)
                {
                    resolution.VerificationEmailError = ex.Message;
                    _logger.LogWarning(ex, "Auto-heal created member but failed to send verification email. email={Email}, token={TransactionToken}",
                        tempMember.Email,
                        transaction.TransactionToken);
                }
            }

            return resolution;
        }

        private static string GetTemporaryPasswordForRegistration(string storedPassword, IDataProtector passwordProtector)
        {
            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                return storedPassword;
            }

            try
            {
                return passwordProtector.Unprotect(storedPassword);
            }
            catch
            {
                return storedPassword;
            }
        }

        private static bool HasLinkedMembership(DB db, Transaction transaction)
        {
            var refs = GetTransactionReferenceCandidates(transaction);
            if (refs.Count == 0)
            {
                return false;
            }

            var normalizedRefs = refs
                .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return db.Memberships
                .AsEnumerable()
                .Any(membership =>
                {
                    var existingRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
                    return existingRefs
                        .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                        .Any(existingRef => normalizedRefs.Contains(existingRef));
                });
        }

        private static List<string> GetTransactionReferenceCandidates(Transaction transaction)
        {
            var refs = new List<string>();
            if (transaction.TransactionId.HasValue && transaction.TransactionId.Value > 0)
            {
                refs.Add(transaction.TransactionId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(transaction.TransactionToken))
            {
                refs.Add(transaction.TransactionToken.Trim());
            }

            return refs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool MembershipContainsTransaction(Membership membership, Transaction transaction)
        {
            var existingRefs = MembershipCancellationHelper.ParseTransactionReferences(membership.transactions);
            if (existingRefs.Count == 0)
            {
                return false;
            }

            var normalizedExisting = existingRefs
                .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetTransactionReferenceCandidates(transaction)
                .Select(MembershipCancellationHelper.NormalizeComparisonValue)
                .Any(candidate => normalizedExisting.Contains(candidate));
        }

        private static async Task<(bool paymentLinked, bool membershipChanged)> EnsureMembershipForTransactionAsync(
            DB db,
            MemberIdentityUser member,
            Transaction transaction,
            bool monthly,
            CancellationToken stoppingToken)
        {
            var membership = await db.Memberships.FirstOrDefaultAsync(x => x.memberID == member.Id, stoppingToken);
            var transactionRefs = GetTransactionReferenceCandidates(transaction);
            var alreadyApplied = membership != null && MembershipContainsTransaction(membership, transaction);

            if (membership != null)
            {
                if (alreadyApplied)
                {
                    return (true, false);
                }

                var nowPlus = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1);
                var expPlus = membership.expiration.AddMonths(monthly ? 1 : 12).AddHours(1);

                membership.expiration = expPlus > nowPlus ? expPlus : nowPlus;
                membership.transactions = (membership.transactions ?? "") + string.Join(";", transactionRefs) + ";";
                membership.phone = string.IsNullOrWhiteSpace(transaction.PayerPhone) ? membership.phone : transaction.PayerPhone;
                membership.isMonthly = monthly;
                membership.isMonthlyActive = monthly;

                db.Memberships.Update(membership);
                return (true, true);
            }

            membership = new Membership
            {
                expiration = DateTime.Now.AddMonths(monthly ? 1 : 12).AddHours(1),
                isMonthly = monthly,
                isMonthlyActive = monthly,
                memberID = member.Id,
                phone = transaction.PayerPhone,
                transactions = string.Join(";", transactionRefs) + ";"
            };

            await db.Memberships.AddAsync(membership, stoppingToken);
            return (true, true);
        }

        private async Task CleanupUnverifiedMembersAsync(DB db, MemberManager memberManager, CancellationToken stoppingToken)
        {
            var toDelete = await db.EmailVerifications
                .Where(x => x.Created.AddHours(24) < DateTime.Now)
                .ToListAsync(stoppingToken);

            foreach (var item in toDelete)
            {
                try
                {
                    var member = await memberManager.FindByEmailAsync(item.Email);
                    if (member == null)
                    {
                        continue;
                    }

                    bool hasLoggedIn = member.LastLoginDateUtc.HasValue;
                    bool hasMembership = db.Memberships.Any(m => m.memberID == member.Id);
                    bool hasSuccessfulTransaction = db.Transactions.Any(t =>
                        t.PayerEmail == item.Email &&
                        (t.StatusCode == 2 || t.Status == "שולם"));

                    if (!member.IsApproved && !hasLoggedIn && !hasMembership && !hasSuccessfulTransaction)
                    {
                        await memberManager.DeleteAsync(member);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean unverified member. email={Email}", item.Email);
                }
            }

            db.EmailVerifications.RemoveRange(toDelete);
            await db.SaveChangesAsync(stoppingToken);
        }

        private static void CleanupNameChanges(DB db)
        {
            var nameChanges = db.NameChanges.Where(x => x.Created.AddYears(1) < DateTime.Now).ToList();
            if (nameChanges.Count > 0)
            {
                db.RemoveRange(nameChanges);
                db.SaveChanges();
            }
        }

        private static string BuildHealingActionSummary(
            bool memberCreated,
            bool membershipChanged,
            bool verificationEmailSent,
            string? verificationEmailError)
        {
            if (memberCreated && membershipChanged && verificationEmailSent)
            {
                return "נוצרו משתמש ומנוי ונשלח אימייל אימות";
            }

            if (memberCreated && membershipChanged && !string.IsNullOrWhiteSpace(verificationEmailError))
            {
                return "נוצרו משתמש ומנוי אך שליחת אימייל האימות נכשלה";
            }

            if (memberCreated && membershipChanged)
            {
                return "נוצרו משתמש ומנוי";
            }

            if (memberCreated)
            {
                return "נוצר משתמש חסר";
            }

            if (membershipChanged)
            {
                return "המנוי שוחזר או עודכן";
            }

            return "בוצעה פעולת ריפוי אוטומטית";
        }

        private sealed class HealingResolutionResult
        {
            public MemberIdentityUser? Member { get; set; }
            public TemporaryMember? TemporaryMember { get; set; }
            public bool MemberCreated { get; set; }
            public bool VerificationEmailSent { get; set; }
            public string VerificationEmailError { get; set; } = string.Empty;
        }
    }
}

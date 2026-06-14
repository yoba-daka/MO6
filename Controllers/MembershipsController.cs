using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyProject12.Models;
using MyProject12.Services;
using MyProject12.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Filters;

namespace MyProject12.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
    [DisableBrowserCache]
    public class MembershipsController : Controller
    {
        private readonly DB _context;
        private readonly IMemberService _memberService;
        private readonly MeshulamService _meshulaService;
        private const int PageSize = 100;

        public MembershipsController(DB context, IMemberService memberService, MeshulamService meshulamService)
        {
            _context = context;
            _memberService = memberService;
            _meshulaService = meshulamService;
        }

        public IActionResult Index(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var result = GetPaginatedMemberships(page, search, statusFilter, typeFilter, advancedFilter, sortBy, sortOrder);
            return View(result);
        }

        public IActionResult LoadMemberships(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var result = GetPaginatedMemberships(page, search, statusFilter, typeFilter, advancedFilter, sortBy, sortOrder);
            return PartialView("Memberships", result);
        }

        [HttpGet]
        public IActionResult DownloadFilteredMemberships(
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var data = GetFilteredMemberships(search, statusFilter, typeFilter, advancedFilter, sortBy, sortOrder);
            return BuildMembershipsExcel("מנויים מסוננים", data, $"מנויים_מסונן_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        [HttpGet]
        public IActionResult DownloadSelectedMemberships(string ids)
        {
            var selectedIds = ParseSelectedIds(ids);
            if (selectedIds.Count == 0)
            {
                return BadRequest("לא נבחרו מנויים לייצוא.");
            }

            var selectedMemberships = _context.Memberships
                .AsNoTracking()
                .Where(x => selectedIds.Contains(x.Id))
                .ToList();

            var mapped = BuildDisplayRows(selectedMemberships)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Email)
                .ToList();

            return BuildMembershipsExcel("מנויים נבחרים", mapped, $"מנויים_נבחרים_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        [HttpGet]
        public IActionResult DownloadAllMemberships(
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var data = GetFilteredMemberships(
                search: null,
                statusFilter: "all",
                typeFilter: "all",
                advancedFilter: "all",
                sortBy: sortBy,
                sortOrder: sortOrder);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("מנויים");
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = "שם";
            ws.Cell(1, 2).Value = "אימייל";
            ws.Cell(1, 3).Value = "טלפון";
            ws.Cell(1, 4).Value = "תוקף";
            ws.Cell(1, 5).Value = "חודשי?";
            ws.Cell(1, 6).Value = "הוראת קבע פעילה?";
            ws.Cell(1, 7).Value = "עסקאות";

            for (var i = 0; i < data.Count; i++)
            {
                var row = i + 2;
                var membership = data[i];

                ws.Cell(row, 1).Value = membership.Name;
                ws.Cell(row, 2).Value = membership.Email;
                ws.Cell(row, 3).Value = membership.Phone;
                ws.Cell(row, 4).Value = membership.Expiration;
                ws.Cell(row, 4).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 5).Value = BoolHe(membership.IsMonthly);
                ws.Cell(row, 6).Value = BoolHe(membership.IsMonthlyActive);
                ws.Cell(row, 7).Value = membership.Transactions;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"מנויים_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        [HttpGet]
        public IActionResult DownloadAllMembers()
        {
            var allUmbracoMembers = _memberService.GetAll(0, int.MaxValue, out _);
            var allCustomMemberships = _context.Memberships.AsNoTracking().ToDictionary(m => m.memberID, m => m);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("משתמשים");
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = "מזהה";
            ws.Cell(1, 2).Value = "שם";
            ws.Cell(1, 3).Value = "אימייל";
            ws.Cell(1, 4).Value = "שם משתמש";
            ws.Cell(1, 5).Value = "מאושר";
            ws.Cell(1, 6).Value = "נעול";
            ws.Cell(1, 7).Value = "תוקף מנוי";
            ws.Cell(1, 8).Value = "מנוי חודשי?";
            ws.Cell(1, 9).Value = "הוראת קבע פעילה?";

            var row = 2;
            foreach (var member in allUmbracoMembers)
            {
                allCustomMemberships.TryGetValue(member.Id.ToString(), out var custom);

                ws.Cell(row, 1).Value = member.Id;
                ws.Cell(row, 2).Value = member.Name;
                ws.Cell(row, 3).Value = member.Email;
                ws.Cell(row, 4).Value = member.Username;
                ws.Cell(row, 5).Value = BoolHe(member.IsApproved);
                ws.Cell(row, 6).Value = BoolHe(member.IsLockedOut);

                if (custom != null)
                {
                    ws.Cell(row, 7).Value = custom.expiration;
                    ws.Cell(row, 7).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                    ws.Cell(row, 8).Value = BoolHe(custom.isMonthly);
                    ws.Cell(row, 9).Value = BoolHe(custom.isMonthlyActive);
                }

                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"משתמשים_{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        [HttpGet]
        public IActionResult GetMemberQuickView(int id)
        {
            var membership = _context.Memberships.AsNoTracking().FirstOrDefault(x => x.Id == id);
            if (membership == null)
            {
                return NotFound("מנוי לא נמצא.");
            }

            var member = GetMemberByMembership(membership);
            var email = member?.Email ?? string.Empty;
            var transactions = ResolveTransactionsForMembership(membership, email);
            var status = ResolveStatus(membership, DateTime.Now);

            var lastSuccess = transactions.FirstOrDefault(_meshulaService.IsSuccessfulTransaction);
            var lastFailure = transactions.FirstOrDefault(x => !_meshulaService.IsSuccessfulTransaction(x));
            var recentRefs = ParseTransactionReferences(membership.transactions).Take(5).ToList();

            return Json(new
            {
                id = membership.Id,
                memberId = membership.memberID,
                name = member?.Name ?? "Unknown Member",
                email = email,
                phone = string.IsNullOrWhiteSpace(membership.phone) ? "לא נמסר" : membership.phone,
                expiration = membership.expiration.ToString("yyyy-MM-dd HH:mm"),
                isMonthly = membership.isMonthly,
                isMonthlyActive = membership.isMonthlyActive,
                statusKey = status.Key,
                statusLabel = status.Label,
                transactionCount = ParseTransactionReferences(membership.transactions).Count,
                directDebitId = transactions.Select(x => x.DirectDebitId).FirstOrDefault(x => x.HasValue && x.Value > 0),
                lastSuccess = lastSuccess == null ? null : new
                {
                    created = lastSuccess.Created.ToString("yyyy-MM-dd HH:mm"),
                    sum = lastSuccess.Sum,
                    status = lastSuccess.Status,
                    token = lastSuccess.TransactionToken,
                    transactionId = lastSuccess.TransactionId,
                    asmachta = lastSuccess.Asmachta
                },
                lastFailure = lastFailure == null ? null : new
                {
                    created = lastFailure.Created.ToString("yyyy-MM-dd HH:mm"),
                    sum = lastFailure.Sum,
                    status = lastFailure.Status,
                    token = lastFailure.TransactionToken,
                    transactionId = lastFailure.TransactionId,
                    asmachta = lastFailure.Asmachta
                },
                recentActions = recentRefs.Select(x => $"שיוך עסקה: {x}")
            });
        }

        [HttpGet]
        public IActionResult GetMembershipTransactions(int id)
        {
            var membership = _context.Memberships.AsNoTracking().FirstOrDefault(x => x.Id == id);
            if (membership == null)
            {
                return NotFound("מנוי לא נמצא.");
            }

            var member = GetMemberByMembership(membership);
            var email = member?.Email ?? string.Empty;
            var transactions = ResolveTransactionsForMembership(membership, email)
                .Take(100)
                .Select(x => new
                {
                    created = x.Created.ToString("yyyy-MM-dd HH:mm"),
                    sum = x.Sum,
                    status = x.Status,
                    statusCode = x.StatusCode,
                    transactionId = x.TransactionId,
                    transactionToken = x.TransactionToken,
                    directDebitId = x.DirectDebitId,
                    asmachta = x.Asmachta,
                    description = x.Description,
                    paymentDate = x.PaymentDate,
                    isSuccess = _meshulaService.IsSuccessfulTransaction(x)
                })
                .ToList();

            return Json(new
            {
                membershipId = membership.Id,
                memberName = member?.Name ?? "Unknown Member",
                items = transactions
            });
        }

        [HttpPost]
        public IActionResult UpdateMembership(int id, string expirationIso, string phone)
        {
            try
            {
                var membership = _context.Memberships.Find(id);
                if (membership == null)
                {
                    return NotFound("מנוי לא נמצא.");
                }

                if (!TryParseExpiration(expirationIso, out var parsedExpiration))
                {
                    return BadRequest("פורמט תאריך לא תקין.");
                }

                membership.expiration = parsedExpiration;
                membership.phone = (phone ?? string.Empty).Trim();

                _context.Memberships.Update(membership);
                _context.SaveChanges();

                var status = ResolveStatus(membership, DateTime.Now);
                return Json(new
                {
                    id = membership.Id,
                    expiration = membership.expiration.ToString("yyyy-MM-dd HH:mm"),
                    expirationIso = membership.expiration.ToString("yyyy-MM-ddTHH:mm:ss"),
                    phone = membership.phone,
                    statusKey = status.Key,
                    statusLabel = status.Label
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"אירעה שגיאה: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult ApplyQuickAction(int id, string action)
        {
            try
            {
                var membership = _context.Memberships.Find(id);
                if (membership == null)
                {
                    return NotFound("מנוי לא נמצא.");
                }

                var normalized = (action ?? string.Empty).Trim().ToLowerInvariant();
                var now = DateTime.Now;

                switch (normalized)
                {
                    case "plusmonth":
                        membership.expiration = (membership.expiration > now ? membership.expiration : now).AddMonths(1);
                        break;
                    case "plusyear":
                        membership.expiration = (membership.expiration > now ? membership.expiration : now).AddMonths(12);
                        break;
                    case "today":
                        membership.expiration = now;
                        break;
                    case "activatemonthly":
                        membership.isMonthly = true;
                        membership.isMonthlyActive = true;
                        break;
                    case "deactivatemonthly":
                        membership.isMonthly = true;
                        membership.isMonthlyActive = false;
                        break;
                    default:
                        return BadRequest("פעולה לא נתמכת.");
                }

                _context.Memberships.Update(membership);
                _context.SaveChanges();

                var status = ResolveStatus(membership, DateTime.Now);
                return Json(new
                {
                    id = membership.Id,
                    expiration = membership.expiration.ToString("yyyy-MM-dd HH:mm"),
                    expirationIso = membership.expiration.ToString("yyyy-MM-ddTHH:mm:ss"),
                    isMonthly = membership.isMonthly,
                    isMonthlyActive = membership.isMonthlyActive,
                    statusKey = status.Key,
                    statusLabel = status.Label
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"אירעה שגיאה: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult UpdateTime(int id, DateTime newTime)
        {
            try
            {
                var membership = _context.Memberships.Find(id);
                if (membership == null)
                {
                    return NotFound();
                }

                membership.expiration = newTime;
                _context.Memberships.Update(membership);
                _context.SaveChanges();
                return Ok();
            }
            catch
            {
                return StatusCode(500, "אירעה שגיאה");
            }
        }

        [HttpPost]
        public IActionResult Create(MembershipViewModel membershipViewModel)
        {
            try
            {
                var member = _memberService.GetByEmail(membershipViewModel.email);
                if (member == null)
                {
                    throw new Exception("Member not found.");
                }

                if (_context.Memberships.Any(x => x.memberID == member.Id.ToString()))
                {
                    throw new Exception("Membership already exists for this member.");
                }

                var membership = new Membership
                {
                    memberID = member.Id.ToString(),
                    isMonthly = false,
                    isMonthlyActive = false,
                    phone = string.Empty,
                    expiration = membershipViewModel.expiration,
                    transactions = string.Empty
                };

                _context.Memberships.Add(membership);
                _context.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"אירעה שגיאה: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelDirectDebit(int id)
        {
            try
            {
                var membership = _context.Memberships.Find(id);
                if (membership == null)
                {
                    return NotFound();
                }

                if (!membership.isMonthly)
                {
                    return StatusCode(400, "המנוי אינו בהוראת קבע.");
                }

                if (!membership.isMonthlyActive)
                {
                    return Ok();
                }

                var parsedRefs = ParseTransactionReferences(membership.transactions);
                var parsedTransactionIds = MembershipCancellationHelper.ParseTransactionIds(parsedRefs);
                var parsedTransactionTokens = MembershipCancellationHelper.ParseTransactionTokens(parsedRefs);

                if (!TryParseMemberId(membership.memberID, out var memberId))
                {
                    throw new Exception("Membership member ID is invalid.");
                }

                var member = _memberService.GetById(memberId);
                if (member == null || string.IsNullOrWhiteSpace(member.Email))
                {
                    throw new Exception("Member email not found.");
                }

                var email = member.Email;
                var normalizedEmail = MembershipCancellationHelper.NormalizeComparisonValue(email);
                var candidateTransactions = _context.Transactions
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
                    var relatedDirectDebitTransactions = _context.Transactions
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
                    email,
                    candidateDirectDebitIds);

                if (transaction == null)
                {
                    throw new Exception("Provider-compatible direct debit transaction not found in DB.");
                }

                if (!await _meshulaService.CancelDirectDebit(transaction, email))
                {
                    return StatusCode(500, "אירעה שגיאה בביטול הוראת הקבע.");
                }

                membership.isMonthlyActive = false;
                _context.Memberships.Update(membership);
                _context.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "אירעה שגיאה בביטול הוראת הקבע.");
            }
        }

        private PaginatedMembershipResult GetPaginatedMemberships(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var filteredQuery = BuildFilteredMembershipQuery(search, statusFilter, typeFilter, advancedFilter, sortBy, sortOrder);
            var kpis = GetMembershipKpis();
            var totalMemberships = filteredQuery.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalMemberships / (double)PageSize));
            var safePage = Math.Clamp(page, 1, totalPages);

            var paginatedMemberships = filteredQuery
                .Skip((safePage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            var rows = BuildDisplayRows(paginatedMemberships);

            return new PaginatedMembershipResult
            {
                Memberships = rows,
                CurrentPage = safePage,
                TotalPages = totalPages,
                TotalItems = totalMemberships,
                KpiTotalMemberships = kpis.TotalMemberships,
                KpiExpired = kpis.Expired,
                KpiMonthlyActive = kpis.MonthlyActive,
                KpiAnnualActive = kpis.AnnualActive,
                Search = search ?? string.Empty,
                StatusFilter = statusFilter ?? "all",
                TypeFilter = typeFilter ?? "all",
                AdvancedFilter = advancedFilter ?? "all",
                SortBy = sortBy ?? "Id",
                SortOrder = sortOrder ?? "desc"
            };
        }

        private List<MembershipDisplayViewModel> GetFilteredMemberships(
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var memberships = BuildFilteredMembershipQuery(search, statusFilter, typeFilter, advancedFilter, sortBy, sortOrder)
                .ToList();
            var rows = BuildDisplayRows(memberships);
            return SortDisplayRows(rows, sortBy, sortOrder);
        }

        private IQueryable<Membership> BuildFilteredMembershipQuery(
            string search = null,
            string statusFilter = "all",
            string typeFilter = "all",
            string advancedFilter = "all",
            string sortBy = "Id",
            string sortOrder = "desc")
        {
            var now = DateTime.Now;
            var normalizedStatus = (statusFilter ?? "all").Trim().ToLowerInvariant();
            var normalizedType = (typeFilter ?? "all").Trim().ToLowerInvariant();
            var normalizedAdvanced = (advancedFilter ?? "all").Trim().ToLowerInvariant();
            var normalizedSortBy = (sortBy ?? "Id").Trim();
            var ascending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            var baseQuery = _context.Memberships.AsNoTracking().AsQueryable();

            if (normalizedStatus == "active")
            {
                baseQuery = baseQuery.Where(m => m.expiration > now);
            }
            else if (normalizedStatus == "inactive")
            {
                baseQuery = baseQuery.Where(m => m.expiration <= now);
            }

            if (normalizedType == "monthly")
            {
                baseQuery = baseQuery.Where(m => m.isMonthly);
            }
            else if (normalizedType == "annual")
            {
                baseQuery = baseQuery.Where(m => !m.isMonthly);
            }

            baseQuery = normalizedAdvanced switch
            {
                "expired" => baseQuery.Where(m => m.expiration <= now),
                "expiring7" => baseQuery.Where(m => m.expiration > now && m.expiration <= now.AddDays(7)),
                "monthlyactive" => baseQuery.Where(m => m.isMonthly && m.isMonthlyActive),
                "monthlyinactive" => baseQuery.Where(m => m.isMonthly && !m.isMonthlyActive),
                "notransactions" => baseQuery.Where(m => m.transactions == null || m.transactions == string.Empty),
                "missingphone" => baseQuery.Where(m => m.phone == null || m.phone == string.Empty),
                "annualactive" => baseQuery.Where(m => !m.isMonthly && m.expiration > now),
                "needsreview" => baseQuery.Where(m => !m.isMonthly && m.expiration > now),
                _ => baseQuery
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim();
                var term = $"%{searchTerm}%";
                var matchingMemberIds = GetMatchingMemberIdsByEmail(searchTerm)
                    .Select(x => x.ToString())
                    .ToArray();
                baseQuery = baseQuery.Where(m =>
                    (m.memberID != null && EF.Functions.Like(m.memberID, term)) ||
                    (m.phone != null && EF.Functions.Like(m.phone, term)) ||
                    (m.transactions != null && EF.Functions.Like(m.transactions, term)) ||
                    (matchingMemberIds.Length > 0 && matchingMemberIds.Contains(m.memberID)));
            }

            return normalizedSortBy switch
            {
                "Expiration" => ascending
                    ? baseQuery.OrderBy(m => m.expiration).ThenBy(m => m.Id)
                    : baseQuery.OrderByDescending(m => m.expiration).ThenByDescending(m => m.Id),
                "Status" => ascending
                    ? baseQuery
                        .OrderBy(m => m.expiration <= now ? 0 : (!m.isMonthly && m.isMonthlyActive ? 1 : (m.isMonthly && m.isMonthlyActive ? 2 : (m.isMonthly ? 3 : 4))))
                        .ThenBy(m => m.expiration)
                        .ThenBy(m => m.Id)
                    : baseQuery
                        .OrderByDescending(m => m.expiration <= now ? 0 : (!m.isMonthly && m.isMonthlyActive ? 1 : (m.isMonthly && m.isMonthlyActive ? 2 : (m.isMonthly ? 3 : 4))))
                        .ThenByDescending(m => m.expiration)
                        .ThenByDescending(m => m.Id),
                "Transactions" => ascending
                    ? baseQuery.OrderBy(m => m.transactions ?? string.Empty).ThenBy(m => m.Id)
                    : baseQuery.OrderByDescending(m => m.transactions ?? string.Empty).ThenByDescending(m => m.Id),
                "Name" => ascending
                    ? baseQuery.OrderBy(m => m.memberID ?? string.Empty).ThenBy(m => m.Id)
                    : baseQuery.OrderByDescending(m => m.memberID ?? string.Empty).ThenByDescending(m => m.Id),
                "Email" => ascending
                    ? baseQuery.OrderBy(m => m.memberID ?? string.Empty).ThenBy(m => m.Id)
                    : baseQuery.OrderByDescending(m => m.memberID ?? string.Empty).ThenByDescending(m => m.Id),
                _ => ascending
                    ? baseQuery.OrderBy(m => m.Id)
                    : baseQuery.OrderByDescending(m => m.Id)
            };
        }

        private static List<MembershipDisplayViewModel> SortDisplayRows(List<MembershipDisplayViewModel> rows, string sortBy, string sortOrder)
        {
            var ascending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            return sortBy switch
            {
                "Name" => ascending ? rows.OrderBy(m => m.Name).ThenBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.Name).ThenByDescending(m => m.Id).ToList(),
                "Email" => ascending ? rows.OrderBy(m => m.Email).ThenBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.Email).ThenByDescending(m => m.Id).ToList(),
                "Expiration" => ascending ? rows.OrderBy(m => m.Expiration).ThenBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.Expiration).ThenByDescending(m => m.Id).ToList(),
                "Status" => ascending ? rows.OrderBy(m => m.StatusLabel).ThenBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.StatusLabel).ThenByDescending(m => m.Id).ToList(),
                "Transactions" => ascending ? rows.OrderBy(m => m.TransactionCount).ThenBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.TransactionCount).ThenByDescending(m => m.Id).ToList(),
                _ => ascending ? rows.OrderBy(m => m.Id).ToList() : rows.OrderByDescending(m => m.Id).ToList()
            };
        }

        private List<MembershipDisplayViewModel> BuildDisplayRows(List<Membership> memberships)
        {
            var parsedMemberships = memberships
                .Select(m =>
                {
                    var parsed = TryParseMemberId(m.memberID, out var memberId) ? memberId : (int?)null;
                    return new { Membership = m, MemberId = parsed };
                })
                .ToList();

            var validMemberIds = parsedMemberships
                .Where(x => x.MemberId.HasValue)
                .Select(x => x.MemberId!.Value)
                .Distinct()
                .ToArray();

            var membersMap = validMemberIds.Length > 0
                ? _memberService.GetAllMembers(validMemberIds).ToDictionary(m => m.Id, m => m)
                : new Dictionary<int, Umbraco.Cms.Core.Models.IMember>();

            var now = DateTime.Now;
            var rows = new List<MembershipDisplayViewModel>(parsedMemberships.Count);

            foreach (var item in parsedMemberships)
            {
                membersMap.TryGetValue(item.MemberId ?? -1, out var member);
                var status = ResolveStatus(item.Membership, now);
                var transactionRefs = ParseTransactionReferences(item.Membership.transactions);

                rows.Add(new MembershipDisplayViewModel
                {
                    Id = item.Membership.Id,
                    MemberId = item.Membership.memberID,
                    Name = member?.Name ?? "Unknown Member",
                    Email = member?.Email ?? "N/A",
                    Phone = item.Membership.phone ?? string.Empty,
                    Expiration = item.Membership.expiration,
                    IsMonthly = item.Membership.isMonthly,
                    IsMonthlyActive = item.Membership.isMonthlyActive,
                    Transactions = item.Membership.transactions ?? string.Empty,
                    StatusLabel = status.Label,
                    StatusKey = status.Key,
                    TransactionCount = transactionRefs.Count
                });
            }

            return rows;
        }

        private IActionResult BuildMembershipsExcel(string sheetName, List<MembershipDisplayViewModel> data, string fileName)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = "שם";
            ws.Cell(1, 2).Value = "אימייל";
            ws.Cell(1, 3).Value = "טלפון";
            ws.Cell(1, 4).Value = "תוקף";
            ws.Cell(1, 5).Value = "סוג";
            ws.Cell(1, 6).Value = "הוראת קבע פעילה?";
            ws.Cell(1, 7).Value = "מצב מנוי";
            ws.Cell(1, 8).Value = "מספר עסקאות";
            ws.Cell(1, 9).Value = "עסקאות";

            for (var i = 0; i < data.Count; i++)
            {
                var row = i + 2;
                var membership = data[i];
                ws.Cell(row, 1).Value = membership.Name;
                ws.Cell(row, 2).Value = membership.Email;
                ws.Cell(row, 3).Value = membership.Phone;
                ws.Cell(row, 4).Value = membership.Expiration;
                ws.Cell(row, 4).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 5).Value = membership.IsMonthly ? "חודשי" : "שנתי";
                ws.Cell(row, 6).Value = BoolHe(membership.IsMonthlyActive);
                ws.Cell(row, 7).Value = membership.StatusLabel;
                ws.Cell(row, 8).Value = membership.TransactionCount;
                ws.Cell(row, 9).Value = string.Join(Environment.NewLine, ParseTransactionReferences(membership.Transactions));
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private static bool TryParseExpiration(string expirationIso, out DateTime parsedExpiration)
        {
            if (DateTimeOffset.TryParse(expirationIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
            {
                parsedExpiration = parsedOffset.LocalDateTime;
                return true;
            }

            if (DateTime.TryParseExact(
                    expirationIso,
                    "yyyy-MM-ddTHH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsedExact))
            {
                parsedExpiration = parsedExact;
                return true;
            }

            if (DateTime.TryParse(expirationIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                parsedExpiration = parsed;
                return true;
            }

            parsedExpiration = default;
            return false;
        }

        private static List<int> ParseSelectedIds(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<int>();
            }

            return ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var parsed) ? parsed : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }

        private static List<string> ParseTransactionReferences(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var separators = new[] { ';', ',', '\n', '\r', '\t', '|' };
            var trimChars = new[] { '"', '\'', '[', ']', '(', ')', '{', '}' };

            return raw
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Trim(trimChars))
                .Where(x => x.Length > 0 && !IsNonTransactionReferenceMarker(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsNonTransactionReferenceMarker(string value)
        {
            var normalized = NormalizeComparisonValue(value);
            return normalized == "חודש-מתנה" ||
                   normalized == "gift-month" ||
                   normalized == "giftmonth";
        }

        private static string NormalizeComparisonValue(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return raw.Trim().Trim('"', '\'', '[', ']', '(', ')', '{', '}').ToLowerInvariant();
        }

        private IEnumerable<int> GetMatchingMemberIdsByEmail(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Enumerable.Empty<int>();
            }

            var normalizedTerm = term.Trim();
            if (normalizedTerm.Length == 0)
            {
                return Enumerable.Empty<int>();
            }

            var ids = new HashSet<int>();

            foreach (var member in _memberService.GetMembersByEmail(normalizedTerm) ?? Enumerable.Empty<IMember>())
            {
                ids.Add(member.Id);
            }

            try
            {
                foreach (var member in _memberService.GetMembersByPropertyValue("email", normalizedTerm, StringPropertyMatchType.Contains) ?? Enumerable.Empty<IMember>())
                {
                    ids.Add(member.Id);
                }
            }
            catch
            {
                // Keep exact-email search support even if the property lookup is unavailable.
            }

            // Fallback for environments where Contains against member properties is not indexed/reliable.
            if (ids.Count == 0 && normalizedTerm.Any(ch => char.IsLetter(ch) || ch == '@' || ch == '.'))
            {
                var allMembers = _memberService.GetAll(0, int.MaxValue, out _);
                foreach (var member in allMembers)
                {
                    if (!string.IsNullOrWhiteSpace(member.Email) &&
                        member.Email.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        ids.Add(member.Id);
                    }
                }
            }

            return ids;
        }

        private List<Transaction> ResolveTransactionsForMembership(Membership membership, string email)
        {
            var refs = ParseTransactionReferences(membership.transactions);
            var numericRefs = refs
                .Select(x => int.TryParse(x, out var parsed) ? parsed : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var tokenRefs = refs
                .Where(x => !int.TryParse(x, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var normalizedTokenRefs = tokenRefs
                .Select(NormalizeComparisonValue)
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();

            var normalizedEmail = NormalizeComparisonValue(email);
            var hasEmail = normalizedEmail.Length > 0;
            if (numericRefs.Count == 0 && normalizedTokenRefs.Count == 0 && !hasEmail)
            {
                return new List<Transaction>();
            }

            return _context.Transactions
                .AsNoTracking()
                .Where(x =>
                    (numericRefs.Count > 0 && x.TransactionId.HasValue && numericRefs.Contains(x.TransactionId.Value)) ||
                    (normalizedTokenRefs.Count > 0 &&
                     !string.IsNullOrWhiteSpace(x.TransactionToken) &&
                     normalizedTokenRefs.Contains(x.TransactionToken.Trim().ToLower())) ||
                    (hasEmail &&
                     !string.IsNullOrWhiteSpace(x.PayerEmail) &&
                     x.PayerEmail.Trim().ToLower() == normalizedEmail))
                .OrderByDescending(x => x.Created)
                .ThenByDescending(x => x.TransactionId.HasValue)
                .Take(250)
                .ToList();
        }

        private IMember? GetMemberByMembership(Membership membership)
        {
            if (!TryParseMemberId(membership.memberID, out var memberId))
            {
                return null;
            }

            return _memberService.GetById(memberId);
        }

        private static bool TryParseMemberId(string rawMemberId, out int memberId)
        {
            return int.TryParse(rawMemberId, out memberId);
        }

        private (int TotalMemberships, int Expired, int MonthlyActive, int AnnualActive) GetMembershipKpis()
        {
            var now = DateTime.Now;
            var memberships = _context.Memberships.AsNoTracking();

            return (
                TotalMemberships: memberships.Count(),
                Expired: memberships.Count(x => x.expiration <= now),
                MonthlyActive: memberships.Count(x => x.isMonthly && x.isMonthlyActive),
                AnnualActive: memberships.Count(x => !x.isMonthly && x.expiration > now)
            );
        }

        private static (string Key, string Label) ResolveStatus(Membership membership, DateTime now)
        {
            if (membership.expiration <= now)
            {
                return ("expired", "פג תוקף");
            }

            if (!membership.isMonthly && membership.isMonthlyActive)
            {
                return ("review", "דורש בדיקה");
            }

            if (membership.isMonthly && membership.isMonthlyActive)
            {
                return ("monthly-active", "חודשי פעיל");
            }

            if (membership.isMonthly && !membership.isMonthlyActive)
            {
                return ("monthly-inactive", "חודשי לא פעיל");
            }

            return ("annual-active", "שנתי פעיל");
        }

        private static string BoolHe(bool? value) => value is bool b ? (b ? "כן" : "לא") : string.Empty;
    }
}

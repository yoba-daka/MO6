using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyProject12.Models;
using MyProject12.Services;
using MyProject12.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Filters;

namespace MyProject12.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
    [DisableBrowserCache]
    public class TransactionsController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly DB _context;
        private readonly MeshulamService _meshulaService;
        private const int PageSize = 100;

        public TransactionsController(DB context, MeshulamService meshulamService)
        {
            _context = context;
            _meshulaService = meshulamService;
        }

        public IActionResult Index(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string sortBy = "Created",
            string sortOrder = "desc")
        {
            var result = GetPaginatedTransactions(page, search, statusFilter, sortBy, sortOrder);
            return View(result);
        }

        public IActionResult LoadTransactions(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string sortBy = "Created",
            string sortOrder = "desc")
        {
            var result = GetPaginatedTransactions(page, search, statusFilter, sortBy, sortOrder);
            return PartialView("Transactions", result);
        }


        [HttpPost]
        public async Task<IActionResult> Refund(int id)
        {
            try
            {
                var transaction = _context.Transactions.Find(id);
                if (transaction == null)
                {
                    return NotFound("עסקה לא נמצאה.");
                }

                if (!transaction.Sum.HasValue)
                {
                    return BadRequest("לא ניתן לזכות עסקה ללא סכום.");
                }

                if (!await _meshulaService.RefundTransaction(
                        transaction,
                        transaction.Sum.Value,
                        transaction.Sum == _meshulaService.monthlyPrice))
                {
                    throw new Exception();
                }

                transaction.Status = "בוטל/זוכה";
                _context.Transactions.Update(transaction);
                _context.SaveChanges();
                return Ok();
            }
            catch
            {
                return StatusCode(500, "אירעה שגיאה");
            }

        }

        private PaginatedTransactionsResult GetPaginatedTransactions(
            int page = 1,
            string search = null,
            string statusFilter = "all",
            string sortBy = "Created",
            string sortOrder = "desc")
        {
            var filteredQuery = BuildFilteredTransactionsQuery(search, statusFilter, sortBy, sortOrder);
            var totalItems = filteredQuery.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            var safePage = Math.Clamp(page, 1, totalPages);

            var transactions = filteredQuery
                .Skip((safePage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            return new PaginatedTransactionsResult
            {
                Transactions = transactions,
                CurrentPage = safePage,
                TotalPages = totalPages,
                TotalItems = totalItems,
                Search = search ?? string.Empty,
                StatusFilter = statusFilter ?? "all",
                SortBy = sortBy ?? "Created",
                SortOrder = sortOrder ?? "desc"
            };
        }

        private IQueryable<Transaction> BuildFilteredTransactionsQuery(
            string search = null,
            string statusFilter = "all",
            string sortBy = "Created",
            string sortOrder = "desc")
        {
            var query = _context.Transactions.AsNoTracking().AsQueryable();
            var normalizedStatus = (statusFilter ?? "all").Trim().ToLowerInvariant();
            var normalizedSortBy = (sortBy ?? "Created").Trim();
            var ascending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            query = normalizedStatus switch
            {
                "success" => query.Where(x => x.Status == "שולם"),
                "refunded" => query.Where(x => x.Status == "בוטל/זוכה"),
                "failed" => query.Where(x => x.Status != "שולם" && x.Status != "בוטל/זוכה"),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var termText = search.Trim();
                var term = $"%{termText}%";
                var hasNumeric = int.TryParse(termText, out var numericTerm);

                query = query.Where(x =>
                    (hasNumeric && x.ID == numericTerm) ||
                    (hasNumeric && x.TransactionId.HasValue && x.TransactionId.Value == numericTerm) ||
                    (hasNumeric && x.ProcessId.HasValue && x.ProcessId.Value == numericTerm) ||
                    (hasNumeric && x.DirectDebitId.HasValue && x.DirectDebitId.Value == numericTerm) ||
                    (x.Status != null && EF.Functions.Like(x.Status, term)) ||
                    (x.FullName != null && EF.Functions.Like(x.FullName, term)) ||
                    (x.PayerPhone != null && EF.Functions.Like(x.PayerPhone, term)) ||
                    (x.PayerEmail != null && EF.Functions.Like(x.PayerEmail, term)) ||
                    (x.Asmachta != null && EF.Functions.Like(x.Asmachta, term)) ||
                    (x.TransactionToken != null && EF.Functions.Like(x.TransactionToken, term)) ||
                    (x.ProcessToken != null && EF.Functions.Like(x.ProcessToken, term)) ||
                    (x.Description != null && EF.Functions.Like(x.Description, term)) ||
                    (x.PaymentDate != null && EF.Functions.Like(x.PaymentDate, term)));
            }

            return normalizedSortBy switch
            {
                "Status" => ascending
                    ? query.OrderBy(x => x.Status ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.Status ?? string.Empty).ThenByDescending(x => x.ID),
                "Sum" => ascending
                    ? query.OrderBy(x => x.Sum ?? 0).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.Sum ?? 0).ThenByDescending(x => x.ID),
                "FullName" => ascending
                    ? query.OrderBy(x => x.FullName ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.FullName ?? string.Empty).ThenByDescending(x => x.ID),
                "Email" => ascending
                    ? query.OrderBy(x => x.PayerEmail ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.PayerEmail ?? string.Empty).ThenByDescending(x => x.ID),
                _ => ascending
                    ? query.OrderBy(x => x.Created).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.Created).ThenByDescending(x => x.ID)
            };
        }
    }
}

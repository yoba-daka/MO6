using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyProject12.Models;
using MyProject12.ViewModels;
using System;
using System.Linq;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Filters;

namespace MyProject12.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
    [DisableBrowserCache]
    public class ContactMessagesController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly DB _context;
        private const int PageSize = 100;

        public ContactMessagesController(DB context)
        {
            _context = context;
        }

        public IActionResult Index(
            int page = 1,
            string search = null,
            string sortBy = "SubmitTime",
            string sortOrder = "desc")
        {
            var result = GetPaginatedContacts(page, search, sortBy, sortOrder);
            return View(result);
        }

        public IActionResult LoadContacts(
            int page = 1,
            string search = null,
            string sortBy = "SubmitTime",
            string sortOrder = "desc")
        {
            var result = GetPaginatedContacts(page, search, sortBy, sortOrder);
            return PartialView("Contacts", result);
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var contact = _context.Contacts.Find(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "הודעה לא נמצאה." });
        }

        private PaginatedContactResult GetPaginatedContacts(
            int page = 1,
            string search = null,
            string sortBy = "SubmitTime",
            string sortOrder = "desc")
        {
            var filteredQuery = BuildFilteredContactsQuery(search, sortBy, sortOrder);
            var totalItems = filteredQuery.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            var safePage = Math.Clamp(page, 1, totalPages);

            var contacts = filteredQuery
                .Skip((safePage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            return new PaginatedContactResult
            {
                Contacts = contacts,
                CurrentPage = safePage,
                TotalPages = totalPages,
                TotalItems = totalItems,
                Search = search ?? string.Empty,
                SortBy = sortBy ?? "SubmitTime",
                SortOrder = sortOrder ?? "desc"
            };
        }

        private IQueryable<Contact> BuildFilteredContactsQuery(
            string search = null,
            string sortBy = "SubmitTime",
            string sortOrder = "desc")
        {
            var query = _context.Contacts.AsNoTracking().AsQueryable();
            var normalizedSortBy = (sortBy ?? "SubmitTime").Trim();
            var ascending = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var termText = search.Trim();
                var term = $"%{termText}%";
                var hasNumeric = int.TryParse(termText, out var numericTerm);

                query = query.Where(x =>
                    (hasNumeric && x.ID == numericTerm) ||
                    (x.name != null && EF.Functions.Like(x.name, term)) ||
                    (x.familyName != null && EF.Functions.Like(x.familyName, term)) ||
                    (x.email != null && EF.Functions.Like(x.email, term)) ||
                    (x.phone != null && EF.Functions.Like(x.phone, term)) ||
                    (x.content != null && EF.Functions.Like(x.content, term)));
            }

            return normalizedSortBy switch
            {
                "Name" => ascending
                    ? query.OrderBy(x => x.name ?? string.Empty).ThenBy(x => x.familyName ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.name ?? string.Empty).ThenByDescending(x => x.familyName ?? string.Empty).ThenByDescending(x => x.ID),
                "Email" => ascending
                    ? query.OrderBy(x => x.email ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.email ?? string.Empty).ThenByDescending(x => x.ID),
                "Phone" => ascending
                    ? query.OrderBy(x => x.phone ?? string.Empty).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.phone ?? string.Empty).ThenByDescending(x => x.ID),
                _ => ascending
                    ? query.OrderBy(x => x.submitTime).ThenBy(x => x.ID)
                    : query.OrderByDescending(x => x.submitTime).ThenByDescending(x => x.ID)
            };
        }
    }
}

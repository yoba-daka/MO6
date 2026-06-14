using System.Collections.Generic;
using MyProject12.Models;

namespace MyProject12.ViewModels
{
    public class PaginatedContactResult
    {
        public IEnumerable<Contact> Contacts { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public string Search { get; set; }
        public string SortBy { get; set; }
        public string SortOrder { get; set; }
    }
}

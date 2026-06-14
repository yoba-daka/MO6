namespace MyProject12.ViewModels
{
    public class MembershipDisplayViewModel
    {
        public int Id { get; set; }
        public string MemberId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime Expiration { get; set; }
        public bool IsMonthly { get; set; }
        public bool IsMonthlyActive { get; set; }
        public string Transactions { get; set; }
        public string StatusLabel { get; set; }
        public string StatusKey { get; set; }
        public int TransactionCount { get; set; }
    }

    public class PaginatedMembershipResult
    {
        public IEnumerable<MembershipDisplayViewModel> Memberships { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int KpiTotalMemberships { get; set; }
        public int KpiExpired { get; set; }
        public int KpiMonthlyActive { get; set; }
        public int KpiAnnualActive { get; set; }
        public string Search { get; set; }
        public string StatusFilter { get; set; }
        public string TypeFilter { get; set; }
        public string AdvancedFilter { get; set; }
        public string SortBy { get; set; }
        public string SortOrder { get; set; }
    }
}

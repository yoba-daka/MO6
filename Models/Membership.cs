using Microsoft.EntityFrameworkCore.Metadata;
using System.ComponentModel.DataAnnotations.Schema;
using Umbraco.Cms.Core.Security;

namespace MyProject12.Models
{
    public class Membership
    {
        public int Id { get; set; }
        public string memberID { get; set; }
        public string phone { get; set; }
        public DateTime expiration { get; set; }
        public bool isMonthly { get; set; }
        public bool isMonthlyActive { get; set; }
        public string transactions { get; set; }

        [NotMapped]
        public MemberIdentityUser member { get; set; }
        [NotMapped]
        public bool Expired { get => expiration < DateTime.Now; }

    }
}

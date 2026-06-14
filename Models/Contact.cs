using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyProject12.Models
{
    [Table("Contacts")]

    public class Contact
    {
        public int ID { get; set; }
        public DateTime submitTime { get; set; }
        public string name { get; set; }
        public string familyName { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string content { get; set; }
    }
}

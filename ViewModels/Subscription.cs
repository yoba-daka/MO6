using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace MyProject12.ViewModels
{
    public class Subscription
    {
        [Required]
        public string FullName { get; set; }
        [Required]
        public string Email { get; set; }

        [DisplayName("טלפון:")]
        [Required(ErrorMessage = "נדרש מספר פלאפון")]
        [MaxLength(10, ErrorMessage = "ארוך מדי")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        [Required]
        public bool Monthly { get; set; }
    }
}

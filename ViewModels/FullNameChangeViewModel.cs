using System.ComponentModel.DataAnnotations;

namespace MyProject12.ViewModels
{
    public class FullNameChangeViewModel
    {
        [Required(ErrorMessage = "נדרש שם מלא")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "נדרשת סיסמה נוכחית")]
        public string CurrentPassword { get; set; }
    }
}

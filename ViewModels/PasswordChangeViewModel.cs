using System.ComponentModel.DataAnnotations;

namespace MyProject12.ViewModels
{
    public class PasswordChangeViewModel
    {
        [Required(ErrorMessage = "נדרשת סיסמה נוכחית")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "נדרשת סיסמה חדשה")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "אישור סיסמה נדרש")]
        [Compare("NewPassword", ErrorMessage = "הסיסמאות אינן תואמות")]
        public string ConfirmNewPassword { get; set; }
    }
}

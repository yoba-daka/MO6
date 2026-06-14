using System.ComponentModel.DataAnnotations;

namespace MyProject12.ViewModels
{
    public class EmailChangeViewModel
    {
        [Required(ErrorMessage = "נדרש אימייל")]
        [EmailAddress(ErrorMessage = "כתובת אימייל לא חוקית")]
        public string Email { get; set; }

        [Required(ErrorMessage = "אישור אימייל נדרש")]
        [Compare("Email", ErrorMessage = "האימיילים אינם תואמים")]
        public string ConfirmEmail { get; set; }

        [Required(ErrorMessage = "נדרשת סיסמה נוכחית")]
        public string CurrentPassword { get; set; }
    }
}

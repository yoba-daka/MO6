using System.ComponentModel.DataAnnotations;

namespace MO6.ViewModels
{
    public class SpecialOfferRegisterViewModel
    {
        [Required(ErrorMessage = "נדרש שם")]
        [RegularExpression(@"^\s*\S+(\s+\S+)+\s*$", ErrorMessage = "נדרש שם מלא - שם ושם משפחה")]
        public string Name { get; set; }

        [Required(ErrorMessage = "נדרשת כתובת אימייל")]
        [EmailAddress(ErrorMessage = "כתובת האימייל אינה חוקית")]
        public string Email { get; set; }

        [Required(ErrorMessage = "נדרש אימות לכתובת אימייל")]
        [Compare("Email", ErrorMessage = "כתובות האימייל אינן תואמות")]
        public string EmailConfirmation { get; set; }

        [Required(ErrorMessage = "נדרשת סיסמא")]
        [MinLength(10, ErrorMessage = "על הסיסמא להיות באורך עשרה תווים לפחות")]
        public string Password { get; set; }

        [Required(ErrorMessage = "נדרשת סיסמא לאימות")]
        [Compare("Password", ErrorMessage = "הסיסמאות אינן תואמות")]
        public string ConfirmPassword { get; set; }

        [Required] // Hidden field can be used, but better to use the name attribute on the checkbox
        public bool AcceptTerms { get; set; }
    }
}
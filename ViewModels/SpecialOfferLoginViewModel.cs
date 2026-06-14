using MO6.ViewModels;
using System.ComponentModel.DataAnnotations;

// It's best practice to put this class in its own file inside the MO6/ViewModels folder.
// I am including it here so the controller method has the complete context.
namespace MO6.ViewModels
{
    public class SpecialOfferLoginViewModel
    {
        [Required(ErrorMessage = "נא להזין כתובת אימייל")]
        [EmailAddress(ErrorMessage = "כתובת אימייל אינה תקינה")]
        public string Email { get; set; }

        [Required(ErrorMessage = "נא להזין סיסמא")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
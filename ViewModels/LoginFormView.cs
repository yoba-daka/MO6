using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace MosheSharon.Models
{
    [Table("Login")]
    public class LoginForm
    {
        [DisplayName("אימייל")]
        [Required(ErrorMessage = "חובה להקליד כתובת אימייל")]
        [EmailAddress(ErrorMessage ="כתובת מייל לא חוקית")]
        [DataType(DataType.EmailAddress)]
        public string email { get; set; }

        [DisplayName("סיסמא")]
        [MaxLength(50, ErrorMessage = "ארוכה מדי")]
        [DataType(DataType.Password)]
        public string password { get; set; }


    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace MyProject12.ViewModels
{
    public class ContactView
    {
        [DisplayName("שם:*")]
        [Required(ErrorMessage = "חובה להקליד שם")]
        [MaxLength(25,ErrorMessage = "השם שהוקלד ארוך מדי")]
        public string name { get; set; }

        [DisplayName("שם משפחה:")]
        [MaxLength(25, ErrorMessage = "השם שהוקלד ארוך מדי")]
        public string familyName { get; set; }

        [DisplayName("אימייל:*")]
        [Required(ErrorMessage = "חובה להקליד כתובת אימייל")]
        [EmailAddress(ErrorMessage ="כתובת מייל לא חוקית")]
        [DataType(DataType.EmailAddress)]
        public string email { get; set; }

        [DisplayName("טלפון:")]
        [MaxLength(10, ErrorMessage = "ארוך מדי")]
        [DataType(DataType.PhoneNumber)]
        public string phone { get; set; }

        [DisplayName("תוכן:*")]
        [Required(ErrorMessage = "נדרש תוכן")]
        [MaxLength(300, ErrorMessage = "תוכן ההודעה מוגבל ל300 תווים")]
        public string content { get; set; }


    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace MyProject12.ViewModels
{
    [Table("Comments")]
    public class CommentView
    {
        public int parentID { get; set; }
        public int? parentCommentID { get; set; }
        public int? userID { get; set; }
        [DisplayName("שם:*")]
        [Required(ErrorMessage = "חובה להקליד שם")]
        [MaxLength(25,ErrorMessage = "השם שהוקלד ארוך מדי")]
        public string name { get; set; }
        [DisplayName("כותרת:*")]
        [Required(ErrorMessage = "נדרשת כותרת")]
        [MaxLength(40, ErrorMessage = "הכותרת שהוקלדה ארוכה מדי")]
        public string title { get; set; }
        [DisplayName("תוכן:*")]
        [Required(ErrorMessage = "נדרש תוכן")]
        [MaxLength(300, ErrorMessage = "תוכן ההודעה מוגבל ל300 תווים")]
        public string content { get; set; }


    }
}
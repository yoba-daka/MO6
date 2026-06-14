using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace MyProject12.Models
{
    [Table("Comments")]
    public class Comment
    {
        public int ID { get; set; }
        public DateTime time { get; set; }
        public int parentID { get; set; }
        public int? parentCommentID { get; set; }
        public int? userID { get; set; }
        public string name { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public bool managment { get; set; }

        public int getIDObf()
        {
            return ID * 65  +108;
        }
        public static int getOriginalID(int obfID)
        {
            return (obfID - 108) / 65;
        }
        public static int getIDObf(int ID)
        {
            return ID * 11 + 54 * (ID + 2);
        }

    }
}
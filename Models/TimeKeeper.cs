namespace MyProject12.Models
{
    public class TimeKeeper
    {
        public int Id { get; set; }
        public DateTime date { get; set; }
        public string memberID { get; set; }
        public int lessonId { get; set; }
        public string time { get; set; }
        public bool isVideo { get; set; }
    }
}

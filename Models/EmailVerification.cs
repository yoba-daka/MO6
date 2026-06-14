namespace MyProject12.Models
{
    public class EmailVerification
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Code { get; set; }
        public int TimesSent { get; set; }
        public DateTime Created { get; set; }
    }
}

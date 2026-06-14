using Microsoft.EntityFrameworkCore;

namespace MyProject12.Models
{
    public class PasswordReset
    {
        public int ID { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public int TimesSent { get; set; }

        public DateTime Created { get; set; }
    }
}

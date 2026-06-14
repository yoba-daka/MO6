namespace MyProject12.Models
{
    public class Comment
    {
        public int ID { get; set; }
    }

    public class Contact
    {
        public int ID { get; set; }
    }

    public class SasToken
    {
        public int Id { get; set; }
        public string MemberId { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
    }

    public class TimeKeeper
    {
        public int Id { get; set; }
        public DateTime date { get; set; }
        public string memberID { get; set; } = string.Empty;
        public int lessonId { get; set; }
        public string time { get; set; } = string.Empty;
        public bool isVideo { get; set; }
    }

    public class EmailVerification
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int TimesSent { get; set; }
        public DateTime Created { get; set; }
    }

    public class PasswordReset
    {
        public int ID { get; set; }
    }

    public class NameChange
    {
        public int ID { get; set; }
        public DateTime Created { get; set; }
    }

    public class Membership
    {
        public int Id { get; set; }
        public string memberID { get; set; } = string.Empty;
        public string phone { get; set; } = string.Empty;
        public DateTime expiration { get; set; }
        public bool isMonthly { get; set; }
        public bool isMonthlyActive { get; set; }
        public string transactions { get; set; } = string.Empty;
    }
}

namespace MosheSharon.Models
{
    public class PlaceholderModel
    {
    }
}

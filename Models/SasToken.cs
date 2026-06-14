namespace MyProject12.Models
{
    public class SasToken
    {
        public int Id { get; set; }
        public string MemberId { get; set; }
        public DateTime TokenExpiration { get; set; }
        public string Token { get; set; }
        public string ContainerName { get; set; }

        public string BlobName { get; set; }
    }
}

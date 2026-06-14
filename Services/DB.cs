using Microsoft.EntityFrameworkCore;
using MO6.Models;
using MosheSharon.Models;
using MyProject12.Models;
using System.Linq;

namespace MyProject12
{
    public class DB : DbContext
    {
        public DB(DbContextOptions<DB> options) : base(options)
        {
        }

        public DbSet<Comment> Comments { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<SasToken> SasTokens { get; set; }
        public DbSet<TimeKeeper> TimeKeepers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Membership> Memberships { get; set; }
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; }
        public DbSet<NameChange> NameChanges { get; set; }
        public DbSet<TemporaryMember> TemporaryMembers { get; set; }



        public TimeKeeper getTimeKeeper(string memberID,int nodeId)
        {
            return TimeKeepers.FirstOrDefault(x => x.memberID == memberID && x.lessonId == nodeId);
        }
        public void AddToken(SasToken token)
        {
            SasTokens.Add(token);
            SaveChanges();
        }


        public SasToken GetValidToken(string containerName, string blobName, string memberId)
        {
            // Get current time to compare with the token expiration
            var now = DateTime.UtcNow;

            // Remove expired tokens for the given blobName
            var expiredTokens = SasTokens.Where(t => t.BlobName == blobName && t.TokenExpiration <= now).ToList();

            if (expiredTokens.Any())
            {
                SasTokens.RemoveRange(expiredTokens);
                SaveChanges();
            }

            return SasTokens.Where(t => t.ContainerName == containerName && t.BlobName == blobName && t.MemberId == memberId && t.TokenExpiration > now)
                           .OrderByDescending(t => t.TokenExpiration) // Get the latest token if multiple valid tokens exist
                           .FirstOrDefault();
        }


    }
}

using System.ComponentModel.DataAnnotations;

namespace MO6.Models
{
    public class TemporaryMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Phone { get; set; }

        [Required]
        public string Token { get; set; } // The unique token we'll pass through cField1

        public DateTime Created { get; set; }

        public bool Processed { get; set; } // Flag to track registration status
    }
}

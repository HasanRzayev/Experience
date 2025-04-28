using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExperienceProject.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Country { get; set; }

        public string? ProfileImage { get; set; }
        public string UserName { get; set; } // Property ismi düzeltildi, Username kaldırıldı.

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public IEnumerable<ExperienceModel>? Experiences { get; set; }

        public ICollection<Like>? Likes { get; set; }

        public ICollection<Follow> Followings { get; set; } = new List<Follow>();
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public ICollection<Notification>? Notifications { get; set; }
    }

}
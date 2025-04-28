using ExperienceProject.Models;
using System.ComponentModel.DataAnnotations;

namespace Experience.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public int ExperienceId { get; set; }
        

        public ExperienceModel Experience { get; set; }

        public int? ParentCommentId { get; set; } // 🔹 Cavab olan şərhlər üçün
        public Comment? ParentComment { get; set; }
        public List<Comment> Replies { get; set; } = new List<Comment>(); // 🔹 Cavabları saxlayır

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // 🔹 Like və Dislike sayları əlavə edilib
        public int LikesCount { get; set; } = 0;
        public int DislikesCount { get; set; } = 0;
    }

}

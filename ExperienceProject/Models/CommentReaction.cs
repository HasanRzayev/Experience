using ExperienceProject.Models;

namespace Experience.Models
{
    public class CommentReaction
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public Comment Comment { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public bool IsLike { get; set; } // true = Like, false = Dislike
    }

}

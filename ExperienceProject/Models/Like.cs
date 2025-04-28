

namespace ExperienceProject.Models
{
    public class Like
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public int ExperienceId { get; set; }
        public ExperienceModel Experience { get; set; }
    }
}

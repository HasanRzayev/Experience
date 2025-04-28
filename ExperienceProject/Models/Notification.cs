using System;

namespace ExperienceProject.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public User User { get; set; }
    }

}
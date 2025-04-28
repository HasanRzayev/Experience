namespace Experience.Models
{
    public class BlockedUser
    {
        public int Id { get; set; }
        public int UserId { get; set; } // Bloklayan istifadəçi
        public int BlockedUserId { get; set; } // Bloklanan istifadəçi
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }

}

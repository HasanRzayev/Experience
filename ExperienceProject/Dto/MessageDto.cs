using System.ComponentModel.DataAnnotations;

namespace Experience.Dto
{
    public class MessageDTO
    {
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public string? MediaUrl { get; set; }  // Fayl linki burda saxlanır
        public string? MediaType { get; set; } // Məsələn: "image/png", "video/mp4"
        public DateTime Timestamp { get; set; }
    }
}

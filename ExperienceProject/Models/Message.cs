using System;
using System.ComponentModel.DataAnnotations;

namespace ExperienceProject.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }  // M?cburi deyil, çünki media mesajlar?nda bo? ola bil?r.

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        public int SenderId { get; set; }
        public User Sender { get; set; }

        [Required]
        public int ReceiverId { get; set; }
        public User Receiver { get; set; }

        public string? MediaUrl { get; set; }  // ??kil, s?s, musiqi fayllar? üçün link
        public string? MediaType { get; set; } // "image", "audio", "video" v? s.
    }

}
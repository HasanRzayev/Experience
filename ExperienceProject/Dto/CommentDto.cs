using System.ComponentModel.DataAnnotations;

namespace Experience.Dto
{
    public class CommentDto
    {
        [Required]
        public string Content { get; set; }

        public int? ParentCommentId { get; set; } // 🔹 Cavab olan şərhlər üçün
    }
    public class CommentResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public int UserId { get; set; }
        public string UserFirstName { get; set; } // Əlavə edildi
        public string UserLastName { get; set; } // Əlavə edildi
        public DateTime CreatedAt { get; set; }
        public int? ParentCommentId { get; set; }
    }


}

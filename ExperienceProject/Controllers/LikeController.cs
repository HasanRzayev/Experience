using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExperienceProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LikesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LikesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> LikeExperience(int id)
        {
            try
            {
                await _context.SaveChangesAsync();
                var userId = GetUserIdFromToken();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Token'da kullanıcı ID'si bulunamadı." });
                }

                Console.WriteLine($"User ID: {userId}, Experience ID: {id}");

                var experience = await _context.Experiences.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
                if (experience == null)
                {
                    return NotFound(new { message = "Deneyim bulunamadı." });
                }


                Console.WriteLine("Deneyim bulundu. Like/unlike işlemine devam ediliyor...");

                var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.UserId == userId.Value && l.ExperienceId == id);
                if (existingLike != null)
                {
                    _context.Likes.Remove(existingLike);
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Like başarıyla kaldırıldı." });
                }

                var like = new Like { UserId = userId.Value, ExperienceId = id };
                _context.Likes.Add(like);

                var notification = new Notification
                {
                    UserId = experience.UserId,
                    Type = "Like",
                    Content = $"Deneyiminiz '{experience.Title}' {userId.Value} tarafından beğenildi.",
                    Date = DateTime.Now
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Like başarıyla eklendi." });
              
                }
                catch (DbUpdateException ex)
                {
                    Console.WriteLine("Veritabanına kaydetme işlemi sırasında bir hata oluştu: " + ex.InnerException?.Message);
                    return StatusCode(500, new { message = "Bir hata oluştu.", details = ex.InnerException?.Message });
                
                Console.WriteLine($"Hata: {ex.Message}");
                return StatusCode(500, new { message = "Bir hata oluştu.", details = ex.Message });
            }
        }


        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetLikeStatus(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var isLiked = await _context.Likes.AnyAsync(l => l.UserId == userId.Value && l.ExperienceId == id);
            return Ok(new { isLiked });
        }

        private int? GetUserIdFromToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }
    
            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return null;
        }

    }
}

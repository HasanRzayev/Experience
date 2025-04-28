namespace Experience.Hubs
{
    using Experience.Dto;
    using Experience.Models;
    using ExperienceProject.Data;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;

    public class CommentHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public CommentHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddComment(int experienceId, CommentDto commentDTO, string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid user token");
                    return;
                }

                var experience = await _context.Experiences.FindAsync(experienceId);
                if (experience == null)
                {
                    await Clients.Caller.SendAsync("Error", "Experience not found");
                    return;
                }

                var comment = new Comment
                {
                    Content = commentDTO.Content,
                    UserId = userId,
                    ExperienceId = experienceId,
                    CreatedAt = DateTime.UtcNow,
                    ParentCommentId = commentDTO.ParentCommentId
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                // Yeni comment real-time olaraq bütün istifadəçilərə göndərilir
                await Clients.Group($"Experience_{experienceId}").SendAsync("ReceiveComment", comment);
            }
            catch (Exception ex)
            {
                // Xətanı serverə log edirik

                // Erroru client-ə göndəririk
                await Clients.Caller.SendAsync("Error", new { message = "Failed to add comment", details = ex.Message });
            }
        }



     
    }

}

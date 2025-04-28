using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExperienceProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

   [HttpGet]
public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
{
    var userId = GetUserIdFromToken();
    if (userId == null)
    {
        return Unauthorized(new { message = "User ID not found in token" });
    }

    var notifications = await _context.Notifications
        .Where(n => n.UserId == userId && !n.IsRead)
        .ToListAsync();

    return Ok(notifications);
}

        [HttpPost("{id}/respond")]
        public async Task<IActionResult> RespondToNotification(int id, [FromBody] FollowRequestResponse response)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            // Debug için log ekleyelim
            Console.WriteLine($"RespondToNotification called with id: {id}, userId: {userId}");

            var followRequest = await _context.FollowRequests
                .FirstOrDefaultAsync(fr => fr.Id == id && fr.FollowedId == userId.Value);

            if (followRequest == null)
            {
                return NotFound(new { message = $"Follow request not found for id {id} and followed user {userId.Value}" });
            }

            if (response.IsAccepted)
            {
                var follow = new Follow
                {
                    FollowerId = followRequest.FollowerId,
                    FollowedId = userId.Value
                };

                _context.Follows.Add(follow);

                var followedUserName = await GetUserNameAsync(userId.Value);

                var notification = new Notification
                {
                    UserId = followRequest.FollowerId,
                    Type = "Follow Request Accepted",
                    Content = $"{followedUserName} accepted your follow request.",
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }

            _context.FollowRequests.Remove(followRequest);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Follow request processed successfully" });
        }

        private async Task<string> GetUserNameAsync(int userId)
{
    var user = await _context.Users.FindAsync(userId);
    return user?.UserName ?? "Unknown";
}

        [HttpPost("{id}/markAsRead")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok();
        }

        private int? GetUserIdFromToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
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

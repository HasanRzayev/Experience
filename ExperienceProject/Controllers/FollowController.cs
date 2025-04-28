using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CloudinaryDotNet;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExperienceProject.Controllers
{
   [Route("api/[controller]")]
[ApiController]
public class FollowersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FollowersController(ApplicationDbContext context)
    {
        _context = context;
    }
        // Bütün follow-lar? s?hif?l?m? (pagination) il? ç?xar
        [HttpGet]
        public async Task<IActionResult> GetAllFollows([FromQuery] int page = 1, [FromQuery] string search = "")
        {
            int pageSize = 10;
            var query = _context.Follows
                .Include(f => f.Follower)
                .Include(f => f.Followed)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(f =>
                    f.Follower.UserName.ToLower().Contains(search) ||
                    f.Follower.FirstName.ToLower().Contains(search) ||
                    f.Follower.LastName.ToLower().Contains(search) ||
                    f.Followed.UserName.ToLower().Contains(search) ||
                    f.Followed.FirstName.ToLower().Contains(search) ||
                    f.Followed.LastName.ToLower().Contains(search));
            }

            var follows = await query

                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new
                {
                    f.Id,
              
                    Follower = new
                    {
                        f.Follower.Id,
                        f.Follower.UserName,
                        f.Follower.FirstName,
                        f.Follower.LastName,
                        f.Follower.ProfileImage
                    },
                    Followed = new
                    {
                        f.Followed.Id,
                        f.Followed.UserName,
                        f.Followed.FirstName,
                        f.Followed.LastName,
                        f.Followed.ProfileImage
                    }
                })
                .ToListAsync();

            return Ok(follows);
        }

        // Follow ID il? sil
        //[HttpDelete("{id}")]
        //public async Task<IActionResult> DeleteFollow(int id)
        //{
        //    var follow = await _context.Follows.FindAsync(id);

        //    if (follow == null)
        //    {
        //        return NotFound("Follow not found.");
        //    }

        //    _context.Follows.Remove(follow);
        //    await _context.SaveChangesAsync();

        //    return Ok("Follow deleted successfully.");
        //}

        [HttpGet("followers")]
        public async Task<IActionResult> GetFollowers()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var followers = await _context.Follows
                .Where(f => f.FollowedId == userId.Value)
                .Select(f => new
                {
                    Id = f.Follower.Id,
                    Username = f.Follower.UserName,
                    ProfileImage = f.Follower.ProfileImage // Profil ??kli varsa
                })
                .ToListAsync();

            return Ok(followers);
        }
        [HttpGet("senders")]
        public async Task<IActionResult> GetMessageSenders()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized();

            var senders = await _context.Messages
                .Where(m => m.ReceiverId == userId) // S?n? g?l?n mesajlar? tap
                .Select(m => m.Sender) // Gönd?r?n user-l?ri seç
                .Distinct() // T?krarlananlar? sil
                .ToListAsync();

            return Ok(senders);
        }

        [HttpPost("{id}/request")]
    public async Task<IActionResult> SendFollowRequest(int id)
    {
        var userId = GetUserIdFromToken();
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var followedUser = await _context.Users.FindAsync(id);
        if (followedUser == null)
        {
            return NotFound();
        }

        var existingRequest = await _context.FollowRequests
            .FirstOrDefaultAsync(f => f.FollowerId == userId.Value && f.FollowedId == id);
        if (existingRequest != null)
        {
            return BadRequest(new { message = "Follow request already sent." });
        }

        var followRequest = new FollowRequest
        {
            FollowerId = userId.Value,
            FollowedId = id,
            Status = FollowRequestStatus.Pending
        };

        _context.FollowRequests.Add(followRequest);

        

        await _context.SaveChangesAsync();

        return Ok();
    }
        [HttpPost("{id}/respond")]
        public async Task<IActionResult> RespondToFollowRequest(int id, [FromBody] FollowRequestResponse response)
        {
            // Token-d?n istifad?çinin ID-sini almaq
            var userId = GetUserIdFromToken(); // Burada token-d?n istifad?çi ID-sini al?r?q

            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            // ?st?k gönd?r?nin ID-si il? uy?un follow request tap?l?r
            var followRequest = await _context.FollowRequests
                .FirstOrDefaultAsync(fr => fr.FollowerId == id && fr.FollowedId == userId.Value ); // Pending (q?bul olunmam??) follow request-l?ri tap?r?q

            // ?g?r follow request tap?lmasa, uy?un follow request yoxdur dem?kdir
            if (followRequest == null)
            {
                return NotFound(new { message = "Follow request not found" });
            }

            // Follow request q?bul edilibs?, Follow yarad?l?r
            if (response.IsAccepted)
            {
                // Yeni follow ?laq?si yarad?l?r
                var follow = new Follow
                {
                    FollowerId = id, // Follow gönd?r?n istifad?çi
                    FollowedId = userId.Value // Follow alan istifad?çi
                };

                _context.Follows.Add(follow); // Yarad?lan follow ?lav? edilir

                // Notification yarad?l?r
                var notification = new Notification
                {
                    UserId = id, // Follow gönd?r?n istifad?çiy? notification gönd?rilir
                    Type = "Follow Request Accepted", // Notification tipi
                    Content = $"{await GetUserNameAsync(userId.Value)} accepted your follow request.", // Notification m?zmunu
                    IsRead = false // Yeni notification oldu?u üçün oxunmam?? statusu verilir
                };

                _context.Notifications.Add(notification); // Notification bazaya ?lav? edilir
            }

            // Follow request silinir
            _context.FollowRequests.Remove(followRequest);
            await _context.SaveChangesAsync(); // D?yi?iklikl?r bazaya yaz?l?r

            return Ok(new { message = "Follow request response processed successfully" });
        }




        [HttpGet("{id}/status")]
    public async Task<IActionResult> GetFollowStatus(int id)
    {
        var userId = GetUserIdFromToken();
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var isFollowing = await _context.Follows.AnyAsync(f => f.FollowerId == userId.Value && f.FollowedId == id);
        var followRequest = await _context.FollowRequests
            .FirstOrDefaultAsync(fr => fr.FollowerId == userId.Value && fr.FollowedId == id);

        if (isFollowing)
        {
            return Ok(new { status = "following" });
        }
        else if (followRequest != null)
        {
            return Ok(new { status = "requested" });
        }
        else
        {
            return Ok(new { status = "follow" });
        }
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
        [HttpDelete("admin/{id}")]
        public async Task<IActionResult> DeleteFollow(int id)
        {
            var follow = await _context.Follows.FirstOrDefaultAsync(f => f.Id == id);

            if (follow == null)
            {
                return NotFound(new { message = "Follow record not found" });
            }

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Follow record deleted successfully" });
        }

        [HttpDelete("{id}")]
    public async Task<IActionResult> UnfollowUser(int id)
    {
        var userId = GetUserIdFromToken();
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in token" });
        }

        var follow = await _context.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == userId.Value && f.FollowedId == id);

        if (follow == null)
        {
            return BadRequest(new { message = "You are not following this user." });
        }

        _context.Follows.Remove(follow);
        await _context.SaveChangesAsync();

        return Ok();
    }
        public class CancelFollowRequestDto
        {

            public int FollowedId { get; set; }
        }

        [HttpPost("cancel-follow-request")]
        public async Task<IActionResult> CancelFollowRequest([FromBody] CancelFollowRequestDto requestDto)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var existingRequest = await _context.FollowRequests
                .FirstOrDefaultAsync(fr => fr.FollowerId == userId && fr.FollowedId == requestDto.FollowedId);

            if (existingRequest == null)
            {
                return BadRequest("Follow request not found.");
            }

            _context.FollowRequests.Remove(existingRequest);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Follow request canceled successfully." });
        }



        [HttpGet("follow-requests")]
        public async Task<IActionResult> GetFollowRequests()
        {
            // Token'd?n istifad?çinin ID-sini ç?xarmaq
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var followRequests = await _context.FollowRequests
                .Include(fr => fr.Follower)  // ?st?k gönd?r?n user-i g?tir
                .Include(fr => fr.Followed)  // ?st?k alan user-i g?tir
                .Where(fr => (fr.FollowedId == userId || fr.FollowerId == userId) && fr.Status == FollowRequestStatus.Pending) // ?stifad?çi ya Follower, ya da Followed olmal?d?r
                .Select(fr => new
                {
                    Id = fr.Id,
                    FollowerId = fr.FollowerId, // ?st?k gönd?r?nin ID-si
                    FollowerUsername = fr.Follower.UserName, // ?st?k gönd?r?nin ad?
                    FollowedId = fr.FollowedId, // ?st?k alan?n ID-si
                })
                .ToListAsync();

            return Ok(followRequests);
        }

        [HttpGet("following")]
        public async Task<IActionResult> GetFollowing()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var following = await _context.Follows
                .Where(f => f.FollowerId == userId.Value)
                .Select(f => new
                {
                    Id = f.Followed.Id,
                    Username = f.Followed.UserName,
                    ProfileImage = f.Followed.ProfileImage // Profil ??kli varsa
                })
                .ToListAsync();

            return Ok(following);
        }

        private async Task<string> GetUserNameAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.UserName ?? "Unknown";
    }
}

}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExperienceProject.Models;
using ExperienceProject.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using Experience.Dto;
using Experience.Models;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MessagesController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private int GetUserIdFromHeader()
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

        return 0;
    }
    [HttpDelete("admin/delete/{messageId}")]
    public async Task<IActionResult> DeleteMessageById(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
        {
            return NotFound("Message not found.");
        }

        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();

        return Ok("Message deleted successfully.");
    }


    [HttpDelete("delete/{receiverId}")]
    public async Task<IActionResult> DeleteMessages(int receiverId)
    {
        var userId = GetUserIdFromHeader();

        var messages = await _context.Messages
            .Where(m => (m.SenderId == userId && m.ReceiverId == receiverId) ||
                        (m.SenderId == receiverId && m.ReceiverId == userId))
            .ToListAsync();

        if (!messages.Any())
        {
            return NotFound("No messages found.");
        }

        _context.Messages.RemoveRange(messages);
        await _context.SaveChangesAsync();

        return Ok("Messages deleted successfully.");
    }
    [HttpGet("isBlocked/{userId}")]
    public async Task<IActionResult> IsUserBlocked(int userId)
    {
        var currentUserId = GetUserIdFromHeader();

        var isBlocked = await _context.BlockedUsers
            .AnyAsync(b => b.UserId == currentUserId && b.BlockedUserId == userId);

        return Ok(new { isBlocked });
    }

    [HttpDelete("unblock/{userId}")]
    public async Task<IActionResult> UnblockUser(int userId)
    {
        var currentUserId = GetUserIdFromHeader();

        var blockEntry = await _context.BlockedUsers
            .FirstOrDefaultAsync(b => b.UserId == currentUserId && b.BlockedUserId == userId);

        if (blockEntry == null)
        {
            return NotFound("User is not blocked.");
        }

        _context.BlockedUsers.Remove(blockEntry);
        await _context.SaveChangesAsync();

        return Ok("User unblocked successfully.");
    }

    [HttpPost("block/{blockedUserId}")]
    public async Task<IActionResult> BlockUser(int blockedUserId)
    {
        var userId = GetUserIdFromHeader();

        if (userId == blockedUserId)
        {
            return BadRequest("You cannot block yourself.");
        }

        var existingBlock = await _context.BlockedUsers
            .FirstOrDefaultAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);

        if (existingBlock != null)
        {
            return BadRequest("User is already blocked.");
        }

        var block = new BlockedUser
        {
            UserId = userId,
            BlockedUserId = blockedUserId
        };

        _context.BlockedUsers.Add(block);
        await _context.SaveChangesAsync();

        return Ok("User blocked successfully.");
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearChat()
    {
        var userId = GetUserIdFromHeader();

        var messages = await _context.Messages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .ToListAsync();

        if (!messages.Any())
        {
            return NotFound("No messages found.");
        }

        _context.Messages.RemoveRange(messages);
        await _context.SaveChangesAsync();

        return Ok("Chat cleared successfully.");
    }
    [HttpGet]
    public async Task<IActionResult> GetMessages()
    {
        var userId = GetUserIdFromHeader();

        var messages = await _context.Messages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderBy(m => m.Timestamp)
            .Include(m => m.Sender)   // Gönd?rici m?lumatlar?
            .Include(m => m.Receiver) // Al?c? m?lumatlar?
            .Select(m => new
            {
                Id = m.Id,
                Content = m.Content,
                MediaType = m.MediaType,
                Timestamp = m.Timestamp,
                Sender = new
                {
                    m.Sender.Id,
                    m.Sender.FirstName,
                    m.Sender.LastName,
                    m.Sender.UserName,
                    m.Sender.ProfileImage
                },
                Receiver = new
                {
                    m.Receiver.Id,
                    m.Receiver.FirstName,
                    m.Receiver.LastName,
                    m.Receiver.UserName,
                    m.Receiver.ProfileImage
                }
            })
            .ToListAsync();

        return Ok(messages);
    }
    [HttpGet("admin")]
    public async Task<IActionResult> GetMessagesForAdmin(int page = 1, int pageSize = 10, string? search = null)
    {
        var query = _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .OrderBy(m => m.Timestamp)
            .AsQueryable();

        // Axtar?? (search) filteri
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Content.Contains(search));
        }

        // S?hif?l?m? t?tbiq et
        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                Id = m.Id,
                Content = m.Content,
                MediaType = m.MediaType,
                Timestamp = m.Timestamp,
                Sender = new
                {
                    m.Sender.Id,
                    m.Sender.FirstName,
                    m.Sender.LastName,
                    m.Sender.UserName,
                    m.Sender.ProfileImage
                },
                Receiver = new
                {
                    m.Receiver.Id,
                    m.Receiver.FirstName,
                    m.Receiver.LastName,
                    m.Receiver.UserName,
                    m.Receiver.ProfileImage
                }
            })
            .ToListAsync();

        return Ok(messages);
    }



    [HttpGet("{receiverId}")]
    public async Task<IActionResult> GetMessagesWithUser(int receiverId)
    {
        var userId = GetUserIdFromHeader();

        var messages = await _context.Messages
            .Where(m => (m.SenderId == userId && m.ReceiverId == receiverId) ||
                        (m.SenderId == receiverId && m.ReceiverId == userId))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Ok(messages);
    }
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] MessageDTO messageDto)
    {
        var userId = GetUserIdFromHeader(); // Token'dan SenderId al
        if (userId == 0)
        {
            return Unauthorized();
        }

        var message = new Message
        {
            SenderId = userId, // Token'dan gelen ID
            ReceiverId = messageDto.ReceiverId,
            Content = messageDto.Content,
            Timestamp = DateTime.UtcNow
        };

        if (ModelState.IsValid)
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return Ok(message);
        }

        return BadRequest(ModelState);
    }

}
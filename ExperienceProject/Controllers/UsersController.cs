namespace ExperienceProject.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExperienceProject.Models;
using ExperienceProject.Data;
using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{

    private readonly ApplicationDbContext _context;
    private readonly Cloudinary _cloudinary;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;

        Account account = new Account(
            "dj997ctyw",
            "278563758399669",
            "HliVZH4iQ8OjiZ_GptjlDeFuDxA");
        _cloudinary = new Cloudinary(account);
    }



    public class UserUpdateDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Country { get; set; }
        public string? UserName { get; set; }
        public string? ProfileImage { get; set; } // Şəkil URL-i əlavə olunur
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateUser([FromBody] UserUpdateDto userDto)
    {
        var userId = GetUserIdFromToken();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound("User not found");

        user.FirstName = userDto.FirstName ?? user.FirstName;
        user.LastName = userDto.LastName ?? user.LastName;
        user.Country = userDto.Country ?? user.Country;
        user.UserName = userDto.UserName ?? user.UserName;
        user.ProfileImage = userDto.ProfileImage ?? user.ProfileImage; // Yeni şəkil URL-i saxlanır

        await _context.SaveChangesAsync();
        return Ok(new { message = "User updated successfully", profileImage = user.ProfileImage });
    }


    [HttpPost("upload-profile-image")]
    public async Task<IActionResult> UploadProfileImage([FromForm] IFormFile file)
    {
        var userId = GetUserIdFromToken();
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound("User not found");

        if (file == null || file.Length == 0) return BadRequest("No file uploaded");

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "profile_pictures"
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        if (uploadResult.Error != null) return BadRequest(uploadResult.Error.Message);

        user.ProfileImage = uploadResult.SecureUrl.ToString();
        await _context.SaveChangesAsync();

        return Ok(new { imageUrl = user.ProfileImage });
    }

    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var currentUserId = GetUserIdFromToken();

        if (currentUserId == null)
        {
            return Unauthorized(); // Token geçerli değil
        }

        return Ok(currentUserId);
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

    [HttpGet]
    public IActionResult GetUsers()
    {
        var currentUserId = GetUserIdFromToken();

        if (currentUserId == null)
        {
            return Unauthorized(); // Token geçersiz
        }

        var users = _context.Users
      .AsNoTracking()
      .Select(u => new
      {
          u.Id,
          u.UserName,
          u.Country,
          u.FirstName,
          u.LastName,
          u.ProfileImage,
          u.Email,
          Experiences = u.Experiences.Select(e => new
          {
              e.Id,
              e.Title,
              e.Description
          }).ToList(),
          Followers = u.Followers.Select(f => new
          {
              f.FollowerId,
              FollowerName = f.Follower.UserName
          }).ToList(),
          Followings = u.Followings.Select(f => new
          {
              f.FollowedId,
              FollowedName = f.Followed.UserName
          }).ToList()
      })
      .ToList();

        return Ok(users);


        return Ok(users);
    }

}

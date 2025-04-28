using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExperienceProject.Dto;
using ExperienceProject.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.EntityFrameworkCore;

namespace ExperienceProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ApplicationDbContext _context;

        public AuthController(AuthService authService,ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }
        private string GetUserName(int userId)
        {
            var user = _context.Users.Find(userId);
            return user?.UserName ?? "Unknown"; // Kullanıcı adı ya da "Unknown" döndür
        }

      
        [HttpGet("GetUserProfile/{userId}")]
        public async Task<IActionResult> GetUserProfile(int userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var experiences = await _context.Experiences
                .Where(e => e.UserId == userId)
                .Include(e => e.ImageUrls) // Include the ImageUrls relationship
                .ToListAsync();

            var userProfile = new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Country,
                user.UserName,
                user.ProfileImage,
                UserExperiences = experiences.Select(e => new
                {
                    e.Title,
                    e.Description,
                    e.Date,
                    e.UserId,
                    e.Location,
                    ImageUrls = e.ImageUrls.Select(img => new
                    {
                        img.Url
                    }).ToList()
                }).ToList()
            };

            return Ok(userProfile);
        }

        [HttpGet("GetProfile")]
        public async Task<IActionResult> GetProfile()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.Replace("Bearer ", "");

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return BadRequest(new { message = "Invalid user ID in token" });
            }

            var user = await _context.Users.FindAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
         
            

            var experiences = await _context.Experiences
                .Where(e => e.UserId == userId)
                .Include(e => e.ImageUrls)
                    .Include(e => e.User) // User-i də gətirmək üçün
                                          // Include the ImageUrls relationship
                .ToListAsync();

            var userProfile = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.Country,
                user.ProfileImage,
                UserExperiences = experiences.Select(e => new ExperienceModel
                {
                    Title = e.Title,
                    Description = e.Description,
                    Date = e.Date,
                    User=e.User,
                    UserId = e.UserId,
                    Location = e.Location,
                    ImageUrls = e.ImageUrls.Select(img => new ExperienceImage
                    {
                        Url = img.Url
                    }).ToList()
                }).ToList()
            };

            // JSON olaraq qaytarmaq üçün:
            return Ok(userProfile);


            return Ok(userProfile);
        }
    
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] UserRegistrationDto userDto)
        {
            if (userDto == null)
            {
                return BadRequest("Veri gelmedi.");
            }
            if (await _context.Users.AnyAsync(u => u.UserName == userDto.UserName))
            {
                return BadRequest(new { message = "Username is already taken" });
            }

            var result = await _authService.RegisterAsync(
                userDto.FirstName,
                userDto.LastName,
                userDto.Email,
                userDto.Password,
                userDto.Country,
                userDto.ProfileImage,
                userDto.UserName 
            );

            if (result.success)
                return Ok(new { Token = result.token, Message = result.message });

            return BadRequest(result.message);
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto userDto)
        {
            var result = await _authService.LoginAsync(userDto.Email, userDto.Password);

            if (result.success)
                return Ok(new { Token = result.token, Message = result.message });

            return BadRequest(result.message);
        }

        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginDto userDto)
        {
            if (userDto.Email.ToLower() != "admin@admin")
            {
                return Unauthorized("Only admin can log in.");
            }

            var result = await _authService.LoginAsync(userDto.Email, userDto.Password);

            if (result.success)
                return Ok(new { Token = result.token, Message = result.message });

            return BadRequest(result.message);
        }

    }
}
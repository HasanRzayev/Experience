using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ExperienceProject.Dto;
using Experience.Models;
using Experience.Dto;

namespace ExperienceProject.Controllers
{

    [Route("api/[controller]")]
    [ApiController]

    public class ExperiencesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;
        public ExperiencesController(ApplicationDbContext context)
        {
            _context = context;
            Account account = new Account(
                "dj997ctyw", 
                "278563758399669", 
                "HliVZH4iQ8OjiZ_GptjlDeFuDxA");

            _cloudinary = new Cloudinary(account);
        }
        [HttpGet("search")]
        public async Task<IActionResult> SearchExperiences([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return Ok(await _context.Experiences
                    .Include(e => e.User)
                    .Include(e => e.ExperienceTags)
                    .ToListAsync());
            }

            var experiences = await _context.Experiences
                .Include(e => e.User)
                .Include(e => e.ImageUrls)
                .Include(e => e.ExperienceTags)
                    .ThenInclude(et => et.Tag) // `Tag` entity-sini daxil et
                .Where(e =>
                    e.Title.Contains(query) ||
                    e.Description.Contains(query) ||
                    e.Location.Contains(query) ||
                    e.User.UserName.Contains(query) || // Burada User.Username əlavə edildi
                    e.ExperienceTags.Any(tag => tag.Tag.TagName.Contains(query)) // `Tag.TagName` istifadə olunur
                )
                .ToListAsync();

            return Ok(experiences);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExperienceModel>>> GetExperiences(int page = 1, int pageSize = 9)
        {
            var experiences = await _context.Experiences
                .Include(e => e.User)
                .Include(e => e.ImageUrls)
                .Include(e => e.ExperienceTags) // Many-to-Many əlaqəsi üçün əsas cədvəl
                    .ThenInclude(et => et.Tag)  // ExperienceTag -> Tag əlaqəsini yükləyirik
                .Include(e => e.Likes)  // Like-ları yüklə
                .Include(e => e.Comments) // Comment-ləri yüklə
                    .ThenInclude(c => c.User) // Comment-ləri yazan istifadəçiləri yüklə
                .OrderByDescending(e => e.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // `await` edirik!

            var result = experiences.Select(e => new
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Date = e.Date,
                Location = e.Location,
                Rating = e.Rating,
                ImageUrls = e.ImageUrls.ToList(),
                TagsName = e.ExperienceTags.Select(et => et.Tag.TagName).ToList(), // Tag-ların adları
                Likes = e.Likes.Count, // Like sayı
                Comments = e.Comments.Select(c => new
                {
                    Id = c.Id,
                    Content = c.Content,
                    Date = c.CreatedAt,
                    User = new
                    {
                        Id = c.User.Id,
                        FirstName = c.User.FirstName,
                        LastName = c.User.LastName,
                        ProfileImage = c.User.ProfileImage,
                        UserName = c.User.UserName
                    }
                }).ToList(),
                User = new
                {
                    Id = e.User.Id,
                    FirstName = e.User.FirstName,
                    LastName = e.User.LastName,
                    ProfileImage = e.User.ProfileImage,
                    UserName = e.User.UserName
                }
            }).ToList();

            return Ok(result);
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
        [HttpGet("{id}")]
        public async Task<ActionResult<ExperienceModel>> GetExperience(int id)
        {
            // İlişkili verileri yüklemek için Include metodunu kullanın
            var experience = await _context.Experiences
                .Include(e => e.User)         // User'ı yükle
                .Include(e => e.ImageUrls)    // ImageUrls koleksiyonunu yükle
                .FirstOrDefaultAsync(e => e.Id == id); // İlgili Experience'ı getirin

            if (experience == null)
            {
                return NotFound();
            }

            // Experience ilə əlaqəli ExperienceTag-ları yükləyirik
            var experienceTags = await _context.ExperienceTags
                .Where(et => et.ExperienceId == id)  // Yalnız bu Experience-ə aid olan Tag-ları gətir
                .Include(et => et.Tag)               // Tag entity-sini də daxil et
                .ToListAsync();

            // JSON nəticəsi üçün obyekt formalaşdırırıq
            var result = new
            {
                Id = experience.Id,
                Title = experience.Title,
                Description = experience.Description,
                Date = experience.Date,
                Location = experience.Location,
                Rating = experience.Rating,
                ImageUrls = experience.ImageUrls.ToList(),
                User = new
                {
                    Id = experience.User.Id,
                    FirstName = experience.User.FirstName,
                    LastName = experience.User.LastName,
                    ProfileImage = experience.User.ProfileImage,
                    UserName = experience.User.UserName
                },
                Tags = experienceTags.Select(et => new
                {
                    Id = et.Tag.TagId,
                    TagName = et.Tag.TagName
                }).ToList()
            };

            return Ok(result);

        }

        // POST: api/Experiences
        // [HttpPost]
        // public async Task<ActionResult<ExperienceModel>> PostExperience(ExperienceModel experience)
        // {
        //     _context.Experiences.Add(experience);
        //     await _context.SaveChangesAsync();
        //
        //     return CreatedAtAction("GetExperience", new { id = experience.Id }, experience);
        // }


        [HttpPost]
public async Task<ActionResult<ExperienceModel>> PostExperienceWithCook([FromForm] ExperienceDto experienceDto)
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

    string dateString = experienceDto.Date.ToString();
    DateTime dateTime;
    bool success = DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTime);

    try
    {
        var experience = new ExperienceModel
        {
            Title = experienceDto.Title,
            Description = experienceDto.Description,
            Location = experienceDto.Location,
            Date = dateTime,
            UserId = userId,  // UserId'yi burada ayarlayın
            ExperienceTags = new List<ExperienceTag>(),
            ImageUrls = new List<ExperienceImage>()
        };

        if (experienceDto.Tags != null)
        {
            var existingTags = await _context.Tags
                .Where(t => experienceDto.Tags.Contains(t.TagName))
                .ToListAsync();

            var newTagNames = experienceDto.Tags.Except(existingTags.Select(t => t.TagName)).ToList();

            foreach (var tagName in newTagNames)
            {
                var newTag = new Tag { TagName = tagName };
                _context.Tags.Add(newTag);
                existingTags.Add(newTag);
            }

            await _context.SaveChangesAsync();

            foreach (var tag in existingTags)
            {
                experience.ExperienceTags.Add(new ExperienceTag
                {
                    Experience = experience,
                    Tag = tag
                });
            }
        }

        if (experienceDto.Images != null && experienceDto.Images.Count > 0)
        {
            foreach (var image in experienceDto.Images)
            {
                using (var stream = image.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(image.FileName, stream),
                        Folder = "experiences"
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        experience.ImageUrls.Add(new ExperienceImage { Url = uploadResult.SecureUrl.AbsoluteUri });
                    }
                    else
                    {
                        return BadRequest($"Resim yükleme başarısız oldu: {uploadResult.Error?.Message}");
                    }
                }
            }
        }

        _context.Experiences.Add(experience);
        await _context.SaveChangesAsync();

        return Ok(experience);
    }
    catch (DbUpdateException ex)
    {
        return StatusCode(500, new { message = "Veritabanı hatası: " + ex.InnerException?.Message });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "Bilinmeyen bir hata oluştu. Lütfen tekrar deneyin." });
    }
}

        [HttpPut("{id}")]
        public async Task<ActionResult<ExperienceModel>> EditExperience(int id, [FromForm] ExperienceDto experienceDto)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "İstifadəçi identifikasiyası tapılmadı." });
            }

            var experience = await _context.Experiences
                .Include(e => e.ExperienceTags)
                .Include(e => e.ImageUrls)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (experience == null)
            {
                return NotFound(new { message = "Experience tapılmadı və ya redaktə etməyə icazəniz yoxdur." });
            }

            // **Məlumatları yenilə**
            experience.Title = experienceDto.Title;
            experience.Description = experienceDto.Description;
            experience.Location = experienceDto.Location;
            experience.Date = experienceDto.Date;

            // **Tag-ları yenilə**
            if (experienceDto.Tags != null)
            {
                _context.ExperienceTags.RemoveRange(experience.ExperienceTags);
                await _context.SaveChangesAsync();

                var existingTags = await _context.Tags
                    .Where(t => experienceDto.Tags.Contains(t.TagName))
                    .ToListAsync();

                var newTagNames = experienceDto.Tags.Except(existingTags.Select(t => t.TagName)).ToList();

                foreach (var tagName in newTagNames)
                {
                    var newTag = new Tag { TagName = tagName };
                    _context.Tags.Add(newTag);
                    await _context.SaveChangesAsync();
                    existingTags.Add(newTag);
                }

                experience.ExperienceTags = existingTags.Select(t => new ExperienceTag
                {
                    ExperienceId = experience.Id,
                    TagId = t.TagId
                }).ToList();
            }

            // **Şəkilləri yenilə (əvvəlki şəkilləri tam sil və yalnız yeni şəkilləri əlavə et)**
            if (experienceDto.Images != null && experienceDto.Images.Count > 0)
            {
                // **Köhnə şəkilləri bazadan sil**
                var oldImages = await _context.ExperienceImages.Where(img => img.ExperienceId == experience.Id).ToListAsync();
                _context.ExperienceImages.RemoveRange(oldImages);
                await _context.SaveChangesAsync();

                var newImageUrls = new List<ExperienceImage>();

                foreach (var image in experienceDto.Images)
                {
                    using (var stream = image.OpenReadStream())
                    {
                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(image.FileName, stream),
                            Folder = "experiences"
                        };

                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                        if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            newImageUrls.Add(new ExperienceImage
                            {
                                ExperienceId = experience.Id, // ID-ni təyin edirik ki, ilişkiləndirilsin
                                Url = uploadResult.SecureUrl.AbsoluteUri
                            });
                        }
                        else
                        {
                            return BadRequest($"Şəkil yüklənmədi: {uploadResult.Error?.Message}");
                        }
                    }
                }

                await _context.ExperienceImages.AddRangeAsync(newImageUrls);
                await _context.SaveChangesAsync();
            }

            _context.Entry(experience).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(experience);
        }



        // DELETE: api/Experiences/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExperience(int id)
        {
            var experience = await _context.Experiences.FindAsync(id);
            if (experience == null)
            {
                return NotFound();
            }

            _context.Experiences.Remove(experience);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        [HttpPost("{experienceId}/comments")]
        public async Task<IActionResult> AddComment(int experienceId, [FromBody] CommentDto commentDto)
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

            var experience = await _context.Experiences.FindAsync(experienceId);
            if (experience == null)
            {
                return NotFound(new { message = "Experience not found" });
            }

            Comment parentComment = null;
            if (commentDto.ParentCommentId.HasValue)
            {
                parentComment = await _context.Comments.FindAsync(commentDto.ParentCommentId.Value);
                if (parentComment == null)
                {
                    return BadRequest(new { message = "Parent comment not found" });
                }
            }

            var comment = new Comment
            {
                Content = commentDto.Content,
                UserId = userId,
                ExperienceId = experienceId,
                CreatedAt = DateTime.UtcNow,
                ParentCommentId = commentDto.ParentCommentId
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(comment);
        }

        // Experience-in Comment-lərini gətirmək
        [HttpGet("{experienceId}/comments")]
        public async Task<IActionResult> GetComments(int experienceId, int page = 1, int pageSize = 5)
        {
            var comments = await _context.Comments
                .Where(c => c.ExperienceId == experienceId && c.ParentCommentId == null)
                .Include(c => c.User)
                .Include(c => c.Replies)
                .ThenInclude(r => r.User)
                .OrderBy(c => c.CreatedAt) // Yorumları tarih sırasına göre getir
                .Skip((page - 1) * pageSize) // Sayfalandırma için yorumları atla
                .Take(pageSize) // Her seferinde alınacak yorum sayısı
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    c.UserId,
                    c.ExperienceId,
                    c.CreatedAt,
                    Likes = _context.CommentReactions.Count(r => r.CommentId == c.Id && r.IsLike),
                    Dislikes = _context.CommentReactions.Count(r => r.CommentId == c.Id && !r.IsLike),
                    User = new
                    {
                        c.User.Id,
                        c.User.UserName,
                        c.User.ProfileImage
                    },
                    Replies = c.Replies.Select(r => new
                    {
                        r.Id,
                        r.Content,
                        r.UserId,
                        r.ExperienceId,
                        r.CreatedAt,
                        Likes = _context.CommentReactions.Count(re => re.CommentId == r.Id && re.IsLike),
                        Dislikes = _context.CommentReactions.Count(re => re.CommentId == r.Id && !re.IsLike),
                        User = new
                        {
                            r.User.Id,
                            r.User.UserName,
                            r.User.ProfileImage
                        }
                    }).ToList()
                })
                .ToListAsync();

            return Ok(comments);
        }


        [HttpPost("comments/{commentId}/react")]
        public async Task<IActionResult> ReactToComment(int commentId, [FromBody] ReactionDto reactionDto)
        {
            if (reactionDto == null)
            {
                return BadRequest(new { message = "Invalid request body" });
            }

            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
            {
                return NotFound(new { message = "Comment not found" });
            }

            var existingReaction = await _context.CommentReactions
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId);

            if (existingReaction != null)
            {
                existingReaction.IsLike = reactionDto.IsLike;
            }
            else
            {
                var reaction = new CommentReaction
                {
                    CommentId = commentId,
                    UserId = userId,
                    IsLike = reactionDto.IsLike
                };
                _context.CommentReactions.Add(reaction);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Reaction updated" });
        }



        [HttpGet("admin/comments")]
        public async Task<IActionResult> GetAdminComments([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var query = _context.Comments
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .AsQueryable();

            // Axtarış filtresi (search varsa)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Content.Contains(search) ||
                                         c.User.FirstName.Contains(search) ||
                                         c.User.LastName.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var comments = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new { totalCount, comments });
        }


        // Comment-i silmək
        [HttpDelete("comments/{commentId}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
            {
                return NotFound(new { message = "Comment not found" });
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Comment deleted successfully" });
        }
        private bool ExperienceExists(int id)
        {
            return _context.Experiences.Any(e => e.Id == id);
        }
    }
}
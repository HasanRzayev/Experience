using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Experience.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TagsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTags([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? tagName = null)
        {
            var query = _context.Tags.AsQueryable();

            if (!string.IsNullOrEmpty(tagName))
            {
                query = query.Where(t => t.TagName.Contains(tagName));
            }

            var tags = await query
                .OrderBy(t => t.TagId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(tags);
        }


        [HttpPost]
        public async Task<IActionResult> AddTag([FromBody] TagCreateDto tagDto)
        {
            if (tagDto == null || string.IsNullOrWhiteSpace(tagDto.TagName))
                return BadRequest("Tag adı boş ola bilməz.");

            var newTag = new Tag { TagName = tagDto.TagName };

            _context.Tags.Add(newTag);
            await _context.SaveChangesAsync();

            var response = new TagResponseDto
            {
                TagId = newTag.TagId,
                TagName = newTag.TagName
            };

            return CreatedAtAction(nameof(GetTags), new { id = newTag.TagId }, response);
        }

        // 🔹 Mövcud tag-ı yenilə
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTag(int id, [FromBody] TagUpdateDto tagDto)
        {
            if (string.IsNullOrWhiteSpace(tagDto.TagName))
                return BadRequest("Tag adı boş ola bilməz.");

            var existingTag = await _context.Tags.FindAsync(id);
            if (existingTag == null)
                return NotFound("Tag tapılmadı.");

            existingTag.TagName = tagDto.TagName;
            await _context.SaveChangesAsync();

            var response = new TagResponseDto
            {
                TagId = existingTag.TagId,
                TagName = existingTag.TagName
            };

            return Ok(response);
        }

        // 🔹 Tag sil (əlaqəli ExperienceTag-ları da sil)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(int id)
        {
            var tag = await _context.Tags
                .Include(t => t.ExperienceTags)
                .FirstOrDefaultAsync(t => t.TagId == id);

            if (tag == null)
                return NotFound("Tag tapılmadı.");

            // Əgər bu tag hər hansı bir Experience-a bağlıdırsa, əlaqəli ExperienceTag-ları sil
            _context.ExperienceTags.RemoveRange(tag.ExperienceTags);
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        public class TagCreateDto
    {
        public string TagName { get; set; }
    }

    public class TagUpdateDto
    {
        public string TagName { get; set; }
    }

    public class TagResponseDto
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
    }

}
}

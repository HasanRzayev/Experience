namespace ExperienceProject.Models;


public class Tag
{
    public int TagId { get; set; }
    public string TagName { get; set; }
    
    public ICollection<ExperienceTag>? ExperienceTags { get; set; }
}

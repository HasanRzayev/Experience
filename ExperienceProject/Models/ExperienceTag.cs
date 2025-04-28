namespace ExperienceProject.Models;

public class ExperienceTag
{
    public int ExperienceId { get; set; }
    public ExperienceModel Experience { get; set; }

    public int TagId { get; set; }
    public Tag Tag { get; set; }
}
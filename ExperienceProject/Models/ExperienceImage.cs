namespace ExperienceProject.Models;

public class ExperienceImage
{
    public int Id { get; set; }
    public string Url { get; set; }
    public int ExperienceId { get; set; }
    public ExperienceModel Experience { get; set; }
}
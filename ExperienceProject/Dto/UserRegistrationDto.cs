namespace ExperienceProject.Dto;

public class UserRegistrationDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string Country { get; set; }
    public IFormFile? ProfileImage { get; set; }
    public string? UserName { get; set; } // New property

}

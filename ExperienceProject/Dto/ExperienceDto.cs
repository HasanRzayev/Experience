using ExperienceProject.Models;

namespace ExperienceProject.Dto;

public class ExperienceDto
{
  
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public List<IFormFile>? Images  { get; set; }   
        public List<string>? Tags { get; set; } // Change from string to List<string>


    }

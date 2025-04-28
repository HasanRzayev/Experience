using Experience.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExperienceProject.Models
{
    public class ExperienceModel
    {
        
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<ExperienceImage>? ImageUrls { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime Date { get; set; }
        public double Rating { get; set; } = 0;

        public ICollection<ExperienceTag> ExperienceTags { get; set; }


        // Add these properties
        public ICollection<Comment>? Comments { get; set; }

        public ICollection<Like>? Likes { get; set; }
        public ICollection<Notification>? Notifications { get; set; }
    }
}
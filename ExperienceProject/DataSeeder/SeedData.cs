using Bogus;
using Experience.Models;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public static class SeedData
{
    private static readonly string UNSPLASH_API_KEY = "sQ7fBK2Unle8Dhb_tI5OtrtDX-sokeZIykHRkvcX-PA";
    private static readonly List<string> IMAGE_CATEGORIES = new() { "travel", "nature", "food", "city", "adventure" };

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var context = new ApplicationDbContext(serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        if (await context.Users.AnyAsync()) return;

        var users = await GetUsersFromApi(40);
        var adminUser = new User
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@admin",
            UserName = "admin",
            Country = "Unknown",
            ProfileImage = "https://img.lovepik.com/png/20231125/man-avatar-image-for-profile-businessman-father-grandfather_693685_wh860.png",
            PasswordHash = HashPassword("admin")
        };

        users.Add(adminUser);
        users.ForEach(u => { if (u.Email != "admin@admin") u.PasswordHash = HashPassword($"Password{u.FirstName}"); });
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        var follows = users
            .Where(follower => follower.Email != "admin@admin")
            .SelectMany(follower =>
                users.Where(followed => followed.Id != follower.Id && followed.Email != "admin@admin")
                     .OrderBy(_ => new Random().Next()).Take(10)
                     .Select(followed => new Follow { FollowerId = follower.Id, FollowedId = followed.Id })
            ).Distinct().ToList();

        await context.Follows.AddRangeAsync(follows);
        await context.SaveChangesAsync();
 
        var tagNames = new List<string>
{
    "Adventure", "Culture", "Food", "Nature", "Historical", "Beach", "Mountains", "Hiking", "Wildlife", "Camping",
    "Luxury", "Backpacking", "Road Trip", "Island", "City Break", "Cruise", "Safari", "Skiing", "Desert", "Trekking",
    "Waterfalls", "Cultural Heritage", "Local Experience", "Diving", "Caving", "Street Food", "Eco Tourism", "Festivals",
    "Hot Springs", "Photography"
};

        var tagFaker = new Faker<Tag>()
            .RuleFor(t => t.TagName, f => f.PickRandom(tagNames));

        var tags = tagFaker.Generate(30);

        await context.Tags.AddRangeAsync(tags);
        await context.SaveChangesAsync();
        var experienceTags = await context.Tags.ToListAsync();
        var userIds = await context.Users.Select(u => u.Id).ToListAsync();
        var experienceData = await GetExperienceDataFromUnsplash();
        var random = new Random();

        var experiences = experienceData.Select(data => new ExperienceModel
        {
            Title = data.Title,
            Description = data.Description,
            Location = data.Location,
            UserId = userIds[random.Next(userIds.Count)],
            Date = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
            Rating = Math.Round((random.NextDouble() * 4 + 1) * 2) / 2, // 0.5-lik intervallarla yuvarlaqlaşdırma
            ImageUrls = data.ImageUrls.Select(url => new ExperienceImage { Url = url }).ToList(),
            ExperienceTags = experienceTags.OrderBy(_ => random.Next()).Take(random.Next(2, 5))
                .Select(tag => new ExperienceTag { TagId = tag.TagId }).ToList()
        }).ToList();


        await context.Experiences.AddRangeAsync(experiences);
        await context.SaveChangesAsync();
        var experiencesList = await context.Experiences.ToListAsync();
        var usersList = await context.Users.ToListAsync();
   
        // Əgər cədvəllər boşdursa, Comment yaratmağa ehtiyac yoxdur
        if (!usersList.Any() || !experiencesList.Any())
        {
            Console.WriteLine("Users və ya Experiences cədvəlləri boşdur. Comment-lər yaradıla bilməz.");
            return;
        }
        var faker = new Faker<Comment>("en")
            .RuleFor(c => c.Content, f => f.Lorem.Sentence())
            .RuleFor(c => c.UserId, f => usersList[random.Next(usersList.Count)].Id)
            .RuleFor(c => c.ExperienceId, f => experiencesList[random.Next(experiencesList.Count)].Id)
            .RuleFor(c => c.CreatedAt, f => f.Date.Past(1));

        // İlk olarak 150 yorum oluştur (ParentCommentId olmadan)
        var comments = faker.Generate(150);

        // Şimdi ParentCommentId'yi güncelle
        foreach (var comment in comments)
        {
            if (random.Next(0, 10) > 7) // %30 ihtimalle ParentCommentId ata
            {
                var possibleParents = comments.Where(x => x.Id != comment.Id).ToList();
                if (possibleParents.Any())
                {
                    comment.ParentCommentId = possibleParents[random.Next(possibleParents.Count)].Id;
                }
            }
        }


        await context.Comments.AddRangeAsync(comments);
        await context.SaveChangesAsync();





        var conversations = new List<List<string>>
            {
                new() { "Hey!", "How are you?", "I'm good, what about you?", "Same here, just chilling.", "What are you up to?", "Nothing much, just watching a movie." },
                new() { "Good morning!", "Good morning! Feeling tired from work.", "I feel you. Want to grab a coffee?", "Sounds like a great idea!" },
                new() { "Good evening!", "Good evening! Are you watching something?", "Yeah, just a series. What about you?", "Just listening to some music." },
                new() { "Sorry, I wanted to text you earlier.", "No worries! What's up?", "I just needed some advice.", "Of course, I'm here for you!" }
            };

        var messages = new List<Message>();
        users = await context.Users.ToListAsync();
        for (int i = 0; i < 5; i++) // Generate 5 different conversations
        {
            var sender = users[random.Next(users.Count)];
            var receiver = users[random.Next(users.Count)];

            while (sender.Id == receiver.Id) // Ensure sender and receiver are different
            {
                receiver = users[random.Next(users.Count)];
            }

            var dialogue = conversations[random.Next(conversations.Count)];
            var timestamp = DateTime.UtcNow.AddDays(-random.Next(1, 30));

            foreach (var message in dialogue)
            {
                messages.Add(new Message
                {
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Content = message,
                    Timestamp = timestamp
                });

                timestamp = timestamp.AddMinutes(random.Next(1, 10)); // Add 1-10 minutes between messages
                (sender, receiver) = (receiver, sender); // Swap sender and receiver for natural conversation flow
            }
        }

        await context.Messages.AddRangeAsync(messages);
        await context.SaveChangesAsync();



    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static async Task<List<User>> GetUsersFromApi(int count)
    {
        using HttpClient client = new HttpClient();
        string response = await client.GetStringAsync($"https://randomuser.me/api/?results={count}");
        return JObject.Parse(response)["results"].Select(result => new User
        {
            FirstName = result["name"]["first"].ToString(),
            LastName = result["name"]["last"].ToString(),
            Country = result["location"]["country"].ToString(),
            ProfileImage = result["picture"]["large"].ToString(),
            UserName = result["login"]["username"].ToString(),
            Email = result["email"].ToString()
        }).ToList();
    }

    private static async Task<List<(string Title, string Description, string Location, List<string> ImageUrls)>> GetExperienceDataFromUnsplash()
    {
        using HttpClient client = new HttpClient();
        var experiences = new List<(string, string, string, List<string>)>();
        var random = new Random();

        foreach (var category in IMAGE_CATEGORIES)
        {
            try
            {
                var response = await client.GetAsync($"https://api.unsplash.com/search/photos?query={category}&per_page=10&client_id={UNSPLASH_API_KEY}");
                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var results = json["results"];

                    foreach (var result in results)
                    {
                        var title = result["description"]?.ToString() ?? "Amazing Experience";
                        var description = result["alt_description"]?.ToString() ?? "A wonderful moment captured.";
                        var location = category + " City";
                        var imageUrls = new List<string> { result["urls"]["regular"].ToString() };
                        experiences.Add((title, description, location, imageUrls));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Xəta baş verdi ({category}): {ex.Message}");
            }
        }

        return experiences;
    }
}

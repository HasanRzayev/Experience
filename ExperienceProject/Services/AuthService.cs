using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ExperienceProject.Data;
using ExperienceProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ExperienceProject.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtHelper _jwtHelper;
        private readonly Cloudinary _cloudinary;

        public AuthService(ApplicationDbContext context, JwtHelper jwtHelper)
        {
            _context = context;
            _jwtHelper = jwtHelper;

            Account account = new Account(
                "dj997ctyw",
                "278563758399669",
                "HliVZH4iQ8OjiZ_GptjlDeFuDxA");

            _cloudinary = new Cloudinary(account);
        }
        
        public async Task<(bool success, string token, string message)> RegisterAsync(
    string firstName, 
    string lastName, 
    string email, 
    string password, 
    string country, 
    IFormFile profileImage, 
    string userName) // Kullanıcı adı ekledik
{
    // Email kontrolü
    if (await _context.Users.AnyAsync(u => u.Email == email))
        return (false, null, "Email adresi zaten kayıtlı.");

    // Kullanıcı adı kontrolü
    if (await _context.Users.AnyAsync(u => u.UserName == userName))
        return (false, null, "Kullanıcı adı zaten alınmış.");

    // Şifre uzunluk kontrolü
    if (password.Length < 8)
        return (false, null, "Şifre en az 8 karakter uzunluğunda olmalıdır.");

            // Şifreyi hashleyelim
            var hashedPassword = HashPassword(password);

            // Profil resmi yükleme işlemi
            string profileImageUrl = null;
    if (profileImage != null)
    {
        using (var stream = profileImage.OpenReadStream())
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(profileImage.FileName, stream),
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = true
            };

            var uploadResult = _cloudinary.Upload(uploadParams);
            if (uploadResult.Error != null)
            {
                return (false, null, $"Resim yükleme hatası: {uploadResult.Error.Message}");
            }

            profileImageUrl = uploadResult.SecureUrl.ToString();
        }
    }

    // Yeni kullanıcı oluşturma
    var user = new User
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        PasswordHash = hashedPassword,

        Country = country,
        ProfileImage = profileImageUrl,
        UserName = userName
    };

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    var token = _jwtHelper.GenerateJwtToken(user.Id, user.Email);
    return (true, token, "Kayıt başarılı.");
}


        public async Task<(bool success, string token, string message)> LoginAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || user.PasswordHash != HashPassword(password))
                return (false, null, "Invalid password.");

            var token = _jwtHelper.GenerateJwtToken(user.Id, user.Email);
            return (true, token, "Login successful.");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
      

        private bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
        {
            using (var hmac = new HMACSHA512(storedSalt))
            {
                byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(storedHash);
            }
        }
    }
}

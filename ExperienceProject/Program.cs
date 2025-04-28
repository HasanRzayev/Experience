using ExperienceProject.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Tasks;
using ExperienceProject.Services;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;
using ExperienceProject.Hubs;
using Experience.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; // Döngüleri engelle
        options.SerializerSettings.Formatting = Formatting.Indented; // JSON'un daha okunabilir olmasını sağlar
    });
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AuthService and JwtHelper to DI container
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JwtHelper>();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings.GetValue<string>("Key"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.GetValue<string>("Issuer"),
        ValidAudience = jwtSettings.GetValue<string>("Audience"),
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddSignalR();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    options.JsonSerializerOptions.MaxDepth = 64; // Default dəyəri artırmaq da olar
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB'a kadar dosya yüklemelerine izin verir
});

builder.Services.AddSignalR(opt =>
{
    opt.EnableDetailedErrors = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Development environment için hata ayıklama sayfasını ekleyin
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Statik dosya servisini etkinleştirin
app.UseRouting();

// Use the configured CORS policy
app.UseCors("AllowSpecificOrigin");
// Tek bir MapHub çağrısı yapın
app.MapHub<MessageHub>("api/hubs/message");

app.UseEndpoints(endpoints =>
{
    // Bu satırı kaldırın
    // endpoints.MapHub<MessageHub>("/hubs/message");
});
app.UseAuthentication();
app.UseAuthorization();
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://*:{port}");

app.MapControllers();

app.UseHttpsRedirection();

// Ensure database migrations are applied
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    // Apply migrations
    await dbContext.Database.MigrateAsync();

    // Seed default users and experiences
    await SeedData.InitializeAsync(services);
}

app.Run();

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using NeoCoffeeServer.Data;
using NeoCoffeeServer.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=coffee.db"));

// JwtService
builder.Services.AddScoped<JwtService>();

// JWT Auth
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var imageFolder = Path.Combine(app.Environment.ContentRootPath, "images");

if (Directory.Exists(imageFolder))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imageFolder),
        RequestPath = "/images"
    });
}

// Создание БД и сидирование
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Создаст файл coffee.db и таблицы, если их нет
    db.Database.EnsureCreated();

    // Добавит стартовых пользователей
    DbSeeder.Seed(db);
}

// Pipeline
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();

app.Run();
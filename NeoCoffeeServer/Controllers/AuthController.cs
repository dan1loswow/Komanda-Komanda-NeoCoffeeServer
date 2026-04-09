using NeoCoffeeServer.Contracts;
using NeoCoffeeServer.Data;
using NeoCoffeeServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace NeoCoffeeServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtService _jwtService;

        public AuthController(AppDbContext db, JwtService jwtService)
        {
            _db = db;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Login and password are required." });
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Login == request.Login);

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            var token = _jwtService.GenerateToken(user);

            var response = new LoginResponse
            {
                Token = token,
                Role = user.Role,
                DisplayName = user.DisplayName
            };

            return Ok(response);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            // Sub claim = user.Id
            var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(subClaim) || !int.TryParse(subClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var user = await _db.Users
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => new
                {
                    u.Id,
                    u.Login,
                    u.DisplayName,
                    u.Role
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Unauthorized(new { message = "User not found." });
            }

            return Ok(user);
        }
    }
}

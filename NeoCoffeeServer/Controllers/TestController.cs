using NeoCoffeeServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NeoCoffeeServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TestController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new
            {
                message = "Server is working",
                time = DateTime.Now
            });
        }

        [HttpGet("users")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users
                .Select(u => new
                {
                    u.Id,
                    u.Login,
                    u.DisplayName,
                    u.Role,
                    u.IsActive
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("admin-only")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdminOnly()
        {
            return Ok(new
            {
                message = "You are admin."
            });
        }
    }
}
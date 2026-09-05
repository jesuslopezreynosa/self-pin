using LocationServer.Models.DTOs;
using LocationServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace LocationServer.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        var user = new User
        {
            Name = req.Name,
            DeviceToken = Guid.NewGuid().ToString("N")
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/admin/users/{user.Id}", user);
    }
}
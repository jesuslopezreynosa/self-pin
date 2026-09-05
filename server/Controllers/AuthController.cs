using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("register-key")]
    public async Task<IActionResult> RegisterKey(
        [FromBody] RegisterKeyRequest req,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null) return Unauthorized();

        user.SigningPublicKey = req.SigningPublicKey;
        await _db.SaveChangesAsync();

        return Ok();
    }
}
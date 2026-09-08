using LocationServer.Extensions;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("register-key")]
    public async Task<IActionResult> RegisterKey([FromBody] RegisterKeyRequest req)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Authorization token missing or invalid." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null)
            return Unauthorized(new { error = "User not found." });

        user.SigningPublicKey = req.SigningPublicKey;
        await _db.SaveChangesAsync();

        return Ok();
    }
}
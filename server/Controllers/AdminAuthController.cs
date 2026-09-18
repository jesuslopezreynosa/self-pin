using System.Security.Claims;
using LocationServer.Models;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminAuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("login/verify")]
    public async Task<IActionResult> VerifyPasskeyAssertion([FromBody] VerifyPasskeyRequest req)
    {
        var credId = Convert.FromBase64String(req.CredentialIdBase64);
        var passkey = await _db.AdminPasskeys
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.CredentialId.SequenceEqual(credId));

        if (passkey == null || !passkey.User.IsAdmin)
            return Unauthorized(new { error = "Invalid passkey or authorization level." });

        // Validate WebAuthn signature against passkey.PublicKey using Fido2 / WebAuthn library...

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, passkey.User.Id.ToString()),
            new Claim(ClaimTypes.Name, passkey.User.Name),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Ok(new { success = true });
    }
}
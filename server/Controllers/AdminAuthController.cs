using System.Security.Claims;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
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
    private readonly IFido2 _fido2;

    public AdminAuthController(AppDbContext db, IFido2 fido2)
    {
        _db = db;
        _fido2 = fido2;
    }

    [HttpPost("login/options")]
    public async Task<IActionResult> GetLoginOptions([FromBody] string username)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == username && u.IsAdmin);

        if (user == null)
            return Unauthorized(new { error = "Admin user not found." });

        var passkeys = await _db.AdminPasskeys
            .AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .ToListAsync();

        var existingKeys = passkeys
            .Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))
            .ToList();

        var optionsParams = new GetAssertionOptionsParams
        {
            AllowedCredentials = existingKeys,
            UserVerification = UserVerificationRequirement.Preferred
        };

        var options = _fido2.GetAssertionOptions(optionsParams);

        // Store options JSON as a string in session
        HttpContext.Session.SetString("fido2.assertionOptions", options.ToJson());

        return Ok(options);
    }

    [HttpPost("login/verify")]
    public async Task<IActionResult> VerifyPasskeyAssertion([FromBody] AuthenticatorAssertionRawResponse clientResponse)
    {
        string? jsonOptions = HttpContext.Session.GetString("fido2.assertionOptions");
        if (string.IsNullOrEmpty(jsonOptions))
            return BadRequest(new { error = "Assertion session expired or invalid request." });

        // Convert stored JSON string to UTF-8 ReadOnlySpan<byte> to satisfy AssertionOptions.FromJson
        byte[] optionsBytes = Encoding.UTF8.GetBytes(jsonOptions);
        AssertionOptions options = AssertionOptions.FromJson(optionsBytes.ToString()!);

        var passkeys = await _db.AdminPasskeys
            .Include(p => p.User)
            .ToListAsync();

        // Safely compare byte arrays for credential matching
        var passkey = passkeys.FirstOrDefault(p =>
            (clientResponse.RawId != null && p.CredentialId.SequenceEqual(clientResponse.RawId)) ||
            (clientResponse.Id != null && p.CredentialId.Equals(clientResponse.Id))
        );

        if (passkey == null || !passkey.User.IsAdmin)
            return Unauthorized(new { error = "Invalid passkey or authorization level." });

        var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams{
            AssertionResponse = clientResponse,
            OriginalOptions = options,
            StoredPublicKey = passkey.PublicKey,
            StoredSignatureCounter = passkey.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = async (args, cancellationToken) => {
                return await _db.Users.AnyAsync(u => u.Id == passkey.UserId, cancellationToken);
            }
        });

        passkey.SignCount = result.SignCount;
        await _db.SaveChangesAsync();

        HttpContext.Session.Remove("fido2.assertionOptions");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, passkey.User.Id.ToString()),
            new Claim(ClaimTypes.Name, passkey.User.Name),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity)
        );

        return Ok(new { success = true });
    }
}
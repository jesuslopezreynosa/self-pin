using LocationServer.Extensions;
using LocationServer.Models;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/location")]
public sealed class LocationController : ControllerBase
{
    private readonly AppDbContext _db;

    public LocationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateLocation([FromBody] EncryptedLocationUpdateRequest req)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null)
            return Unauthorized();

        var location = new LocationEntry
        {
            UserId = user.Id,
            EncryptedPayload = req.EncryptedPayload,
            KeyVersion = req.KeyVersion,
            Timestamp = req.Timestamp ?? DateTime.UtcNow
        };

        user.LastUpdated = location.Timestamp;

        _db.Locations.Add(location);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed()
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Authorization token header required." });

        var requestingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == token);

        if (requestingUser == null)
            return Unauthorized(new { error = "Invalid device token." });

        var userGroupIds = await _db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == requestingUser.Id)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        if (!userGroupIds.Any())
            return Ok(Array.Empty<object>());

        var sharedUserIds = await _db.GroupMembers
            .AsNoTracking()
            .Where(gm => userGroupIds.Contains(gm.GroupId))
            .Select(gm => gm.UserId)
            .Distinct()
            .ToListAsync();

        var sharedFeed = await _db.Users
            .AsNoTracking()
            .Where(u => sharedUserIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.LastUpdated,
                LatestEntry = _db.Locations
                    .AsNoTracking()
                    .Where(l => l.UserId == u.Id)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => new { l.EncryptedPayload, l.KeyVersion, l.Timestamp })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(sharedFeed);
    }

    [HttpGet("map-config")]
    public async Task<IActionResult> GetMapConfig([FromServices] IConfiguration configuration)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(new { message = "Device token is required." });

        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.DeviceToken == token);

        if (!userExists)
            return Unauthorized(new { message = "Unauthorized: Device token is not registered." });

        var cartoApiKey = configuration["CartoDb:ApiKey"] ?? string.Empty;

        return Ok(new
        {
            cartoApiKey,
            tileUrlTemplate = "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png"
        });
    }
}
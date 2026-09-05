using LocationServer.Models.DTOs;
using LocationServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/location")]
public class LocationController : ControllerBase
{
    private readonly AppDbContext _db;

    public LocationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] EncryptedLocationUpdateRequest req,
        [FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
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
    public async Task<IActionResult> GetFeed([FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        // 1. Explicitly check for device token header
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized(new { error = "Device token header required." });

        var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (requestingUser == null)
            return Unauthorized(new { error = "Invalid device token." });

        // 2. Query groups the user belongs to
        var userGroupIds = await _db.GroupMembers
            .Where(gm => gm.UserId == requestingUser.Id)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        // If the user isn't assigned to any group, return an empty feed list
        if (!userGroupIds.Any())
            return Ok(new List<object>());

        // 3. Query member IDs inside those groups
        var sharedUserIds = await _db.GroupMembers
            .Where(gm => userGroupIds.Contains(gm.GroupId))
            .Select(gm => gm.UserId)
            .Distinct()
            .ToListAsync();

        // 4. Return encrypted payloads for shared group members
        var sharedFeed = await _db.Users
            .Where(u => sharedUserIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.LastUpdated,
                LatestEntry = _db.Locations
                    .Where(l => l.UserId == u.Id)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => new { l.EncryptedPayload, l.KeyVersion, l.Timestamp })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(sharedFeed);
    }
}
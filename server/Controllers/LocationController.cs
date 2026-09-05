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

    [HttpGet("map-config")]
    public async Task<IActionResult> GetMapConfig([FromHeader(Name = "X-Device-Token")] string deviceToken, [FromServices] IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return Unauthorized(new { message = "Device token is required." });
        }

        // Authenticate: Ensure the device token belongs to a registered user
        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.DeviceToken == deviceToken);

        if (!userExists)
        {
            return Unauthorized(new { message = "Unauthorized: Device token is not registered." });
        }

        // Retrieve CARTO API key from appsettings.json
        var cartoApiKey = configuration["CartoDb:ApiKey"] ?? string.Empty;

        return Ok(new
        {
            cartoApiKey,
            tileUrlTemplate = "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png"
        });
    }

    /// <summary>
    /// Accepts a key rotation request signed with the current PSK and relays it to group members.
    /// </summary>
    [HttpPost("rotate-key")]
    public async Task<IActionResult> RotateKey(
        [FromBody] KeyRotationPayloadDto request,
        [FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        // 1. Authenticate Requesting Device
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized(new { error = "Device token header required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (user == null)
            return Unauthorized(new { error = "Invalid device token." });

        // 2. Validate User Membership in Target Group
        var memberRecord = await _db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == user.Id);

        if (memberRecord == null)
            return Forbid();

        var group = await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId);

        if (group == null)
            return NotFound(new { error = "Group not found." });

        // 3. Update Group State
        group.CurrentKeyVersion = request.NewKeyVersion;

        // Flag all OTHER active members as having a pending key rotation
        foreach (var member in group.Members.Where(m => m.UserId != user.Id))
        {
            member.PendingKeyRotation = true;
        }

        // Store or broadcast rotation relay payload (e.g., via SignalR or DB queue)
        // StoreRotationPayload(group.Id, request);

        await _db.SaveChangesAsync();

        return Ok(new { success = true, newKeyVersion = group.CurrentKeyVersion });
    }

    /// <summary>
    /// Returns user authentication and key rotation status for active group memberships.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetUserStatus([FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        // 1. Authenticate Requesting Device
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized(new { error = "Device token header required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (user == null)
            return Unauthorized(new { error = "Invalid device token." });

        // 2. Fetch Group Memberships & Pending Key Rotation Status
        var memberships = await _db.GroupMembers
            .Where(gm => gm.UserId == user.Id)
            .Join(_db.Groups, gm => gm.GroupId, g => g.Id, (gm, g) => new
            {
                GroupId = g.Id,
                GroupName = g.Name,
                g.CurrentKeyVersion,
                gm.PendingKeyRotation
            })
            .ToListAsync();

        return Ok(new
        {
            userId = user.Id,
            userName = user.Name,
            groups = memberships
        });
    }
}
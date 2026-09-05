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
    /// Serves POST /api/v1/location/rotate-key
    /// Receives client-generated HMAC rotation payload and relays it to other group members.
    /// </summary>
    [HttpPost("rotate-key")]
    public async Task<IActionResult> RotateKey(
        [FromBody] KeyRotationPayloadDto request,
        [FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized(new { error = "Device token header required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (user == null)
            return Unauthorized(new { error = "Invalid device token." });

        var isMember = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == user.Id);
        if (!isMember)
            return Forbid();

        var group = await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId);

        if (group == null)
            return NotFound(new { error = "Group not found." });

        // Update group key state and payload relay
        group.CurrentKeyVersion = request.NewKeyVersion;
        group.EncryptedNewKey = request.EncryptedNewKey;
        group.Signature = request.Signature;

        // Flag remaining active group members to pick up the new key
        foreach (var member in group.Members)
        {
            if (member.UserId == user.Id)
            {
                member.PendingKeyRotation = false; // Sender is already up-to-date
            }
            else
            {
                member.PendingKeyRotation = true;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { success = true, newKeyVersion = group.CurrentKeyVersion });
    }

    /// <summary>
    /// Serves GET /api/v1/location/status
    /// Provides active group states, pending rotation flags, and rotation payloads for client processing.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetUserStatus([FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken))
            return Unauthorized(new { error = "Device token header required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (user == null)
            return Unauthorized(new { error = "Invalid device token." });

        var memberships = await _db.GroupMembers
            .Where(gm => gm.UserId == user.Id)
            .Join(_db.Groups, gm => gm.GroupId, g => g.Id, (gm, g) => new
            {
                GroupId = g.Id,
                GroupName = g.Name,
                g.CurrentKeyVersion,
                gm.PendingKeyRotation,
                g.EncryptedNewKey,
                g.Signature
            })
            .ToListAsync();

        var groupStatuses = memberships.Select(m => new
        {
            groupId = m.GroupId.ToString(),
            groupName = m.GroupName,
            currentKeyVersion = m.CurrentKeyVersion,
            pendingKeyRotation = m.PendingKeyRotation,
            rotationPayload = (m.PendingKeyRotation && !string.IsNullOrEmpty(m.EncryptedNewKey)) ? new
            {
                groupId = m.GroupId.ToString(),
                newKeyVersion = m.CurrentKeyVersion,
                encryptedNewKey = m.EncryptedNewKey,
                signature = m.Signature
            } : null
        });

        return Ok(new
        {
            userId = user.Id,
            userName = user.Name,
            groups = groupStatuses
        });
    }


}
using LocationServer.Models.DTOs;
using LocationServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/groups")]
public class GroupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public GroupsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Fetches the latest encrypted group PSK envelopes for the authenticated user.
    /// </summary>
    [HttpGet("keys")]
    public async Task<IActionResult> GetGroupKeys([FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null) return Unauthorized();

        var userGroupIds = await _db.GroupMembers
            .Where(gm => gm.UserId == user.Id)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        var latestKeys = await _db.GroupKeys
            .Where(gk => userGroupIds.Contains(gk.GroupId) && gk.UserId == user.Id)
            .GroupBy(gk => gk.GroupId)
            .Select(g => g.OrderByDescending(k => k.KeyVersion).First())
            .Select(gk => new GroupKeyResponseDto(
                gk.GroupId,
                gk.KeyVersion,
                gk.EncryptedPsk,
                gk.CreatedAt
            ))
            .ToListAsync();

        return Ok(new { group_keys = latestKeys });
    }

    /// <summary>
    /// Accepts client-generated PSK envelopes encrypted for each group member.
    /// Server simply stores these envelopes without being able to decrypt them.
    /// </summary>
    [HttpPost("keys")]
    public async Task<IActionResult> PostGroupKeys(
        [FromBody] PostGroupKeysRequest req,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null) return Unauthorized();

        var isMember = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == req.GroupId && gm.UserId == user.Id);
        if (!isMember) return Forbid();

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == req.GroupId);
        if (group == null) return NotFound("Group not found.");

        group.CurrentKeyVersion = req.NewKeyVersion;

        foreach (var env in req.Envelopes)
        {
            _db.GroupKeys.Add(new GroupKey
            {
                GroupId = req.GroupId,
                UserId = env.UserId,
                KeyVersion = req.NewKeyVersion,
                EncryptedPsk = env.EncryptedPsk,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, currentKeyVersion = group.CurrentKeyVersion });
    }

    /// <summary>
    /// Returns member public keys for a group so a client can generate and encrypt new group PSK envelopes.
    /// </summary>
    [HttpGet("{groupId}/member-keys")]
    public async Task<IActionResult> GetMemberPublicKeys(
        Guid groupId,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null) return Unauthorized();

        var isMember = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);
        if (!isMember) return Forbid();

        var members = await _db.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Join(_db.Users, gm => gm.UserId, u => u.Id, (gm, u) => new
            {
                u.Id,
                u.Name,
                u.SigningPublicKey
            })
            .ToListAsync();

        return Ok(members);
    }
}
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
    /// Creates a new location sharing group and adds the requesting user as a member.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req, [FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (user == null) return Unauthorized();

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            CurrentKeyVersion = 1
        };

        _db.Groups.Add(group);

        // Add creator as initial group member
        _db.GroupMembers.Add(new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = user.Id
        });

        await _db.SaveChangesAsync();

        return Created($"/api/v1/groups/{group.Id}", new
        {
            id = group.Id,
            name = group.Name,
            currentKeyVersion = group.CurrentKeyVersion
        });
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

        // Fetch keys for the user's groups into memory first to avoid EF Core LINQ translation limits
        var allKeys = await _db.GroupKeys
            .Where(gk => userGroupIds.Contains(gk.GroupId) && gk.UserId == user.Id)
            .ToListAsync();

        var latestKeys = allKeys
            .GroupBy(gk => gk.GroupId)
            .Select(g => g.OrderByDescending(k => k.KeyVersion).First())
            .Select(gk => new GroupKeyResponseDto(
                gk.GroupId,
                gk.KeyVersion,
                gk.EncryptedPsk,
                gk.CreatedAt
            ))
            .ToList();

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
        if (!isMember) return StatusCode(StatusCodes.Status403Forbidden);

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == req.GroupId);
        if (group == null) return NotFound("Group not found.");

        group.CurrentKeyVersion = req.KeyVersion;

        foreach (var env in req.Envelopes)
        {
            _db.GroupKeys.Add(new GroupKey
            {
                GroupId = req.GroupId,
                UserId = env.UserId,
                KeyVersion = req.KeyVersion,
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
        if (!isMember) return StatusCode(StatusCodes.Status403Forbidden);

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

    /// <summary>
    /// Adds a user to a group using their device token.
    /// </summary>
    [HttpPost("{groupId}/members")]
    public async Task<IActionResult> AddMember(
        Guid groupId,
        [FromBody] AddMemberRequest req,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        var token = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var requestingUser = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == token);
        if (requestingUser == null) return Unauthorized();

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == req.DeviceToken);

        if (group == null || targetUser == null)
            return NotFound("Group or target user not found.");

        var isAlreadyMember = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == targetUser.Id);
        if (!isAlreadyMember)
        {
            _db.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = targetUser.Id
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    public record AddMemberRequest(string DeviceToken);
}
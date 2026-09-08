using LocationServer.Extensions;
using LocationServer.Models;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("api/v1/groups")]
public sealed class GroupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public GroupsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req)
    {
        var token = Request.GetBearerToken();
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

    [HttpGet("keys")]
    public async Task<IActionResult> GetGroupKeys()
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == token);

        if (user == null) return Unauthorized();

        var userGroupIds = await _db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == user.Id)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        if (!userGroupIds.Any())
            return Ok(new { group_keys = Array.Empty<GroupKeyResponseDto>() });

        var allKeys = await _db.GroupKeys
            .AsNoTracking()
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

    [HttpPost("keys")]
    public async Task<IActionResult> PostGroupKeys([FromBody] PostGroupKeysRequest req)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == token);

        if (user == null) return Unauthorized();

        var isMember = await _db.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == req.GroupId && gm.UserId == user.Id);

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

    [HttpGet("{groupId}/member-keys")]
    public async Task<IActionResult> GetMemberPublicKeys(Guid groupId)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == token);

        if (user == null) return Unauthorized();

        var isMember = await _db.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

        if (!isMember) return StatusCode(StatusCodes.Status403Forbidden);

        var members = await _db.GroupMembers
            .AsNoTracking()
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

    [HttpPost("{groupId}/members")]
    public async Task<IActionResult> AddMember(Guid groupId, [FromBody] AddMemberRequest req)
    {
        var token = Request.GetBearerToken();
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var requestingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == token);

        if (requestingUser == null) return Unauthorized();

        var group = await _db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId);

        var targetUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DeviceToken == req.DeviceToken);

        if (group == null || targetUser == null)
            return NotFound("Group or target user not found.");

        var isAlreadyMember = await _db.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == targetUser.Id);

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
}
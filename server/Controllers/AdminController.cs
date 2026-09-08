using LocationServer.Models;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // --- User Management ---

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        var user = new User
        {
            Name = req.Name,
            DeviceToken = Guid.NewGuid().ToString("N")
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/admin/users/{user.Id}", user);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .ToListAsync();

        return Ok(users);
    }

    // --- Group Management ---

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            CurrentKeyVersion = 1
        };

        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        return Created($"/admin/groups/{group.Id}", group);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _db.Groups
            .AsNoTracking()
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.CurrentKeyVersion,
                Members = _db.GroupMembers
                    .Where(gm => gm.GroupId == g.Id)
                    .Join(_db.Users, gm => gm.UserId, u => u.Id, (gm, u) => new
                    {
                        u.Id,
                        u.Name
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(groups);
    }

    [HttpPost("groups/assign")]
    public async Task<IActionResult> AssignUserToGroup([FromBody] AssignUserGroupRequest req)
    {
        var group = await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == req.GroupId);

        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == req.UserId);

        if (group == null || !userExists)
            return NotFound(new { error = "Group or User not found." });

        var existingMembership = group.Members.FirstOrDefault(gm => gm.UserId == req.UserId);

        if (existingMembership == null)
        {
            _db.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                UserId = req.UserId
            });

            TriggerGroupKeyRotation(group);
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true, currentKeyVersion = group.CurrentKeyVersion });
    }

    [HttpPost("groups/remove")]
    public async Task<IActionResult> RemoveUserFromGroup([FromBody] AssignUserGroupRequest req)
    {
        var group = await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == req.GroupId);

        if (group == null)
            return NotFound(new { error = "Group not found." });

        var membership = group.Members.FirstOrDefault(gm => gm.UserId == req.UserId);

        if (membership != null)
        {
            _db.GroupMembers.Remove(membership);
            TriggerGroupKeyRotation(group, evictedUserId: req.UserId);
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true, currentKeyVersion = group.CurrentKeyVersion });
    }

    private static void TriggerGroupKeyRotation(Group group, int? evictedUserId = null)
    {
        group.CurrentKeyVersion += 1;
    }
}
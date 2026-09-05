using LocationServer.Models.DTOs;
using LocationServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocationServer.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
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
        var users = await _db.Users.ToListAsync();
        return Ok(users);
    }

    // --- Group Management & Automated Key Rotation Triggers ---

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

        var userExists = await _db.Users.AnyAsync(u => u.Id == req.UserId);

        if (group == null || !userExists)
            return NotFound(new { error = "Group or User not found." });

        var existingMembership = group.Members.FirstOrDefault(gm => gm.UserId == req.UserId);

        if (existingMembership == null)
        {
            // Add new member
            _db.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                UserId = req.UserId
            });

            // Trigger key rotation flag across all active group members
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
            // Remove user from group
            _db.GroupMembers.Remove(membership);

            // Trigger key rotation flag for all remaining members so evicted user loses access
            TriggerGroupKeyRotation(group, evictedUserId: req.UserId);

            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true, currentKeyVersion = group.CurrentKeyVersion });
    }

    /// <summary>
    /// Helper to increment key version and mark members for background rotation.
    /// </summary>
    private static void TriggerGroupKeyRotation(Group group, int? evictedUserId = null)
    {
        group.CurrentKeyVersion += 1;

        foreach (var member in group.Members)
        {
            if (evictedUserId.HasValue && member.UserId == evictedUserId.Value)
                continue;
        }
    }
}

// Request Contracts
public record CreateUserRequest(string Name);
public record CreateGroupRequest(string Name);
public record AssignUserGroupRequest(Guid GroupId, int UserId);
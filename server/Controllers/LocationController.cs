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
    public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateRequest req, [FromHeader(Name = "X-Device-Token")] string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken);
        if (user == null)
        {
            return Unauthorized();
        }

        var location = new LocationEntry
        {
            UserId = user.Id,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            Accuracy = req.Accuracy,
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
        var latestLocations = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.LastUpdated,
                Location = _db.Locations
                    .Where(l => l.UserId == u.Id)
                    .OrderByDescending(l => l.Timestamp)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(latestLocations);
    }
}
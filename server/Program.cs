using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=location.db"));

// Enable CORS for public mobile updates and local frontend testing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Automatically create SQLite DB schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ----------------------------------------------------
// PUBLIC API: Receive Location Updates from Client App
// ----------------------------------------------------
app.MapPost("/api/v1/location/update", async (LocationUpdateRequest req, HttpContext context, AppDbContext db) =>
{
    // Extract X-Device-Token header for simple authentication
    if (!context.Request.Headers.TryGetValue("X-Device-Token", out var deviceToken) || string.IsNullOrEmpty(deviceToken))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.DeviceToken == deviceToken.ToString());
    if (user == null)
    {
        return Results.Unauthorized();
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

    db.Locations.Add(location);
    await db.SaveChangesAsync();

    return Results.Ok(new { success = true });
});

// ----------------------------------------------------
// PUBLIC/PRIVATE API: Read Latest Locations for Family Map
// ----------------------------------------------------
app.MapGet("/api/v1/location/feed", async (AppDbContext db) =>
{
    var latestLocations = await db.Users
        .Select(u => new
        {
            u.Id,
            u.Name,
            u.LastUpdated,
            Location = db.Locations
                .Where(l => l.UserId == u.Id)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefault()
        })
        .ToListAsync();

    return Results.Ok(latestLocations);
});

// ----------------------------------------------------
// PRIVATE ADMIN API: Manage Users (Network/Tailscale restricted)
// ----------------------------------------------------
app.MapPost("/admin/users", async (CreateUserRequest req, AppDbContext db) =>
{
    var user = new User
    {
        Name = req.Name,
        DeviceToken = Guid.NewGuid().ToString("N") // Generate unique token
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/admin/users/{user.Id}", user);
});

app.Run();

// ----------------------------------------------------
// EF Core Data Context & Entities
// ----------------------------------------------------
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<LocationEntry> Locations => Set<LocationEntry>();
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class LocationEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public DateTime Timestamp { get; set; }
}

public record LocationUpdateRequest(double Latitude, double Longitude, double Accuracy, DateTime? Timestamp);
public record CreateUserRequest(string Name);
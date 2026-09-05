using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=location.db"));

// Register Controller support
builder.Services.AddControllers();

// Enable CORS
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

// Map Controller Endpoints automatically
app.MapControllers();

// Automatically create SQLite DB schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Wipe and recreate the database ONLY during local development
    if (app.Environment.IsDevelopment())
    {
        db.Database.EnsureDeleted(); // Wipes existing database file/schema
    }

    db.Database.EnsureCreated(); // Creates clean schema
}

app.Run();
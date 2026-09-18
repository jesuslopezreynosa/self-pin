using Fido2NetLib;
using LocationServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=location.db"));

builder.Services.AddControllers();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AdminSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Memory Cache & Session for FIDO2 challenge persistence
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "WebAuthnSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Direct IFido2 registration (bypasses AddFido2 extension method binding issues)
builder.Services.AddSingleton<IFido2>(sp => new Fido2(new Fido2Configuration
{
    ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost",
    ServerName = "SelfPin Admin",
    Origins = new HashSet<string> { builder.Configuration["Fido2:Origin"] ?? "https://localhost:7078" }
}));

var app = builder.Build();

app.UseCors();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.EnsureDeleted();
    }
    db.Database.EnsureCreated();
}

app.Run();
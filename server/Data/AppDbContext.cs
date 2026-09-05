using LocationServer.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<LocationEntry> Locations => Set<LocationEntry>();
    public DbSet<Group> Groups => Set<Group>();
}
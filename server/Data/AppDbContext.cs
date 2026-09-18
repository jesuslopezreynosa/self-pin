using LocationServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LocationServer;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupKey> GroupKeys => Set<GroupKey>();
    public DbSet<LocationEntry> Locations => Set<LocationEntry>();
    public DbSet<AdminPasskey> AdminPasskeys => Set<AdminPasskey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Fast authentication lookup O(1) by device token
        modelBuilder.Entity<User>()
            .HasIndex(u => u.DeviceToken)
            .IsUnique();

        // Unique index on WebAuthn Credential ID
        modelBuilder.Entity<AdminPasskey>()
            .HasIndex(p => p.CredentialId)
            .IsUnique();

        modelBuilder.Entity<AdminPasskey>()
            .HasOne(p => p.User)
            .WithMany(u => u.Passkeys)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate membership and speed up group member joins
        modelBuilder.Entity<GroupMember>()
            .HasIndex(gm => new { gm.GroupId, gm.UserId })
            .IsUnique();

        // Index for fetching user's latest group keys efficiently
        modelBuilder.Entity<GroupKey>()
            .HasIndex(gk => new { gk.UserId, gk.GroupId, gk.KeyVersion });

        // Index for user feed location lookups ordered by timestamp
        modelBuilder.Entity<LocationEntry>()
            .HasIndex(le => new { le.UserId, le.Timestamp });
    }
}
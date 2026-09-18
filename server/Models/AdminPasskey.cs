namespace LocationServer.Models;

public sealed class AdminPasskey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public uint SignCount { get; set; }
    public string? AaGuid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
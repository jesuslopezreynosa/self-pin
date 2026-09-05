namespace LocationServer.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty; // Unique bearer credential
    public string? SigningPublicKey { get; set; }          // Hex-encoded 32-byte public key
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
namespace LocationServer.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string? SigningPublicKey { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public ICollection<AdminPasskey> Passkeys { get; set; } = new List<AdminPasskey>();
}
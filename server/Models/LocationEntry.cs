namespace LocationServer.Models;

public sealed class LocationEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string EncryptedPayload { get; set; } = string.Empty;    // Ciphertext payload string encrypted client-side via AES-256-GCM
    public int KeyVersion { get; set; } = 1;    // Tracks key version used so clients know if they can decrypt it
    public DateTime Timestamp { get; set; }
}
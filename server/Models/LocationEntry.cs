namespace LocationServer.Models;

public class LocationEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }

    // Ciphertext payload string encrypted client-side via AES-256-GCM
    public string EncryptedPayload { get; set; } = string.Empty;

    // Tracks key version used so clients know if they can decrypt it
    public int KeyVersion { get; set; } = 1;
    public DateTime Timestamp { get; set; }
}
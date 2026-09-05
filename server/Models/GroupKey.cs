namespace LocationServer.Models;

public class GroupKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public int UserId { get; set; }
    public int KeyVersion { get; set; }
    public string EncryptedPsk { get; set; } = string.Empty; // Encrypted client-side via member's SigningPublicKey
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
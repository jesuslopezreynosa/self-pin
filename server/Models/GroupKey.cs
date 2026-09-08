namespace LocationServer.Models;

public sealed class GroupKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public int UserId { get; set; }
    public int KeyVersion { get; set; }
    public string EncryptedPsk { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
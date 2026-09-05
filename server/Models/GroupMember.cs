namespace LocationServer.Models;

public class GroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    
    // Flag to indicate the client needs to re-encrypt with a new key
    public bool PendingKeyRotation { get; set; } = false;
}
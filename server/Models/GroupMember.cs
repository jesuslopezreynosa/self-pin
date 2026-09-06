namespace LocationServer.Models;

public class GroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public int UserId { get; set; }
    // public bool PendingKeyRotation { get; set; } = false;
}
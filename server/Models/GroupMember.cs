namespace LocationServer.Models;

public sealed class GroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public int UserId { get; set; }
}
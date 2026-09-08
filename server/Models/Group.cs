namespace LocationServer.Models;

public sealed class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentKeyVersion { get; set; } = 1;
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}
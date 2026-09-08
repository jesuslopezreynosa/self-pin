namespace LocationServer.Models.DTOs;

public sealed record CreateGroupRequest(string Name);
public sealed record AssignUserGroupRequest(Guid GroupId, int UserId);
public sealed record AddMemberRequest(string DeviceToken);

public sealed record GroupMemberKeyEnvelope(
    int UserId,
    string EncryptedPsk
);

public sealed record PostGroupKeysRequest(
    Guid GroupId,
    int KeyVersion,
    List<GroupMemberKeyEnvelope> Envelopes
);

public sealed record GroupKeyResponseDto(
    Guid GroupId,
    int KeyVersion,
    string EncryptedPsk,
    DateTime CreatedAt
);
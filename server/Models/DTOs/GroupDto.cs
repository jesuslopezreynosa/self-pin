namespace LocationServer.Models.DTOs;

public record CreateGroupRequest(string Name);

public record GroupMemberKeyEnvelope(
    int UserId,
    string EncryptedPsk
);

public record PostGroupKeysRequest(
    Guid GroupId,
    int KeyVersion,
    List<GroupMemberKeyEnvelope> Envelopes
);

public record GroupKeyResponseDto(
    Guid GroupId,
    int KeyVersion,
    string EncryptedPsk,
    DateTime CreatedAt
);
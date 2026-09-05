namespace LocationServer.Models.DTOs;

public record RegisterKeyRequest(string SigningPublicKey);

public record GroupKeyResponseDto(
    Guid GroupId,
    int KeyVersion,
    string EncryptedPsk,
    DateTime UpdatedAt
);

public record GroupMemberKeyEnvelope(
    int UserId,
    string EncryptedPsk
);

public record PostGroupKeysRequest(
    Guid GroupId,
    int NewKeyVersion,
    List<GroupMemberKeyEnvelope> Envelopes
);

// public record AssignUserGroupRequest(Guid GroupId, int UserId);
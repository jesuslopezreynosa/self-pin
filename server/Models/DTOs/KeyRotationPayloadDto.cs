namespace LocationServer.Models.DTOs;

public sealed record KeyRotationPayloadDto(
    Guid GroupId,
    int NewKeyVersion,
    string EncryptedNewKey,
    string Signature
);
namespace LocationServer.Models.DTOs;

public record KeyRotationPayloadDto(
    Guid GroupId,
    int NewKeyVersion,
    string EncryptedNewKey,
    string Signature
);
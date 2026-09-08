namespace LocationServer.Models.DTOs;

public sealed record EncryptedLocationUpdateRequest(
    string EncryptedPayload,
    int KeyVersion,
    DateTime? Timestamp
);
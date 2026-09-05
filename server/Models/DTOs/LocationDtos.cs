namespace LocationServer.Models.DTOs;

public record EncryptedLocationUpdateRequest(
    string EncryptedPayload, 
    int KeyVersion, 
    DateTime? Timestamp
);

public record CreateUserRequest(string Name);
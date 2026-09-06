namespace LocationServer.Models.DTOs;

public record RegisterKeyRequest(string SigningPublicKey);

public record CreateUserRequest(string Name);
namespace LocationServer.Models.DTOs;

public sealed record RegisterKeyRequest(string SigningPublicKey);
public sealed record CreateUserRequest(string Name);
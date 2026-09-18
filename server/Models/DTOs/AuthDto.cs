namespace LocationServer.Models.DTOs;

public sealed record RegisterKeyRequest(string SigningPublicKey);
public sealed record CreateUserRequest(string Name);

// Admin Passkey Request Contracts
public sealed record PasskeyOptionsResponse(
    string Challenge,
    string RpId,
    string UserId
);

public sealed record RegisterPasskeyRequest(
    string CredentialIdBase64,
    string PublicKeyBase64,
    string AttestationObjectBase64
);

public sealed record VerifyPasskeyRequest(
    string CredentialIdBase64,
    string AuthenticatorDataBase64,
    string SignatureBase64,
    string ClientDataJsonBase64
);
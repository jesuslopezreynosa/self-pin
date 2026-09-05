# Architecture Plan: Server-Side PSK & Dynamic Key Rotation

To support End-to-End Encryption (E2EE) using a Pre-Shared Key (PSK) while treating the backend purely as a relay and authentication server, the server must manage access rights and instruct clients to rotate keys without having access to the key material itself.

## Key Server Responsibilities

Zero-Knowledge Key Storage: The .NET server never generates, reads, or stores the PSK in plain text or cipher form.

Group Key Versioning: The server tracks a monotonic key version counter (`KeyVersion: int`) per group.

Payload Relay: Stores and returns raw encrypted string payloads (`EncryptedPayload: string`) attached to their respective `KeyVersion`.

Roster Change Triggers: Automatically flags group members to perform a key rotation whenever group membership changes.

## Database Schema Updates (EF Core / SQLite)

``` C#
public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    
    // Increments every time a member joins, leaves, or triggers a key rotation
    public int CurrentKeyVersion { get; set; } = 1;
    
    public ICollection<GroupMember> Members { get; set; }
}

public class GroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    
    // Tracks if this specific client needs to fetch/re-encrypt using a new key
    public bool PendingKeyRotation { get; set; } = false;
}

public class LocationUpdate
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EncryptedPayload { get; set; } // AES-256-GCM encrypted payload from client
    public int KeyVersion { get; set; }          // Version of the key used to encrypt
    public DateTime Timestamp { get; set; }
}
```

## Group Membership Change Workflow (Join / Leave)

When a user joins or leaves a group, the server updates the state to signal client-side key re-negotiation:

1. Member Event Triggered:
        - A user leaves, is removed, or a new user is invited to the group.

2. Server Updates Group Metadata:
        - Increment `Group.CurrentKeyVersion += 1`.
        - Set `PendingKeyRotation = true` for all remaining valid group members.
        - If a user was removed, revoke their authorization tokens so they cannot fetch new encrypted payloads.

3. Client Notification:
        - The next time active clients check in or receive a SignalR/WebSocket event, the server passes `CurrentKeyVersion` and `PendingKeyRotation = true`.

## Key Exchange & Rotation Protocol (Out-of-Band / Peer-to-Peer)

Because the server cannot generate or hold the PSK:

```text
[ Active Client A ]             [ .NET Backend ]             [ Joining Client B ]
        │                              │                              │
        │ ─── 1. Member Joined ──────► │                              │
        │                              │ ◄─── 2. Fetch Group State ── │
        │ ◄── 3. Flag: Rotation Req ── │                              │
        │                              │                              │
        │ ══════════════ 4. Direct Out-of-Band Key Exchange ═════════► │
        │                  (e.g., QR Code / Direct Peer Transfer)     │
        │                              │                              │
        │ ─── 5. Upload Payload (v2) ► │                              │
        │                              │ ◄─── 6. Fetch Payload (v2) ─ │
```

1. Generating the New Key: An existing online device (e.g., Device A) generates a new random 256-bit key locally using `crypto.getRandomValues()`.

2. Transferring to Members:
        - In-Person / QR Code: Device A displays a QR code containing the new PSK for Device B to scan.
        - Encrypted Relay Payload: Alternatively, Device A uses a temporary ECDH public key from Device B to securely encrypt and relay the new PSK through the server without exposing it.

3. Acknowledgment: Once devices adopt the new PSK associated with `CurrentKeyVersion`, they update their local storage and start encrypting outgoing location updates with the new key version.

## Implementation Instructions for Implementation Session

- [ ] Data Model: Update EF Core entities (`Group`, `GroupMember`, `LocationUpdate`) to include `KeyVersion` and `PendingKeyRotation`.
- [ ] API Endpoints:
        - Adjust `POST /api/v1/location/update` to accept `EncryptedPayload` and `KeyVersion` strings instead of raw coordinates.
        - Add `PendingKeyRotation` status check to the user status endpoint.
- [ ] Admin Controller Actions: Update the logic in `AdminController` so that `RemoveUserFromGroup` or `AddUserToGroup` automatically increments `CurrentKeyVersion` and marks remaining members for rotation.
- [ ] Error Handling: If a client submits a location using an outdated `KeyVersion`, respond with `HTTP 409 Conflict` (or custom code) along with the `CurrentKeyVersion` to prompt the client to request/import the latest key.
# Architecture Plan: Server-Side PSK & Dynamic Key Rotation

To support End-to-End Encryption (E2EE) using a Pre-Shared Key (PSK) while treating the backend purely as a relay and authentication server, the server manages access rights and instructs clients to rotate keys without having access to the key material itself.

## Summary

* **Zero-Knowledge Architecture:** Location data is encrypted on the client using AES-256-GCM before transmission. The server stores and relays raw ciphertext without ever seeing plain-text coordinates or encryption keys.
* **Roster Changes Trigger Version Bumps:** When users join or leave a group via `AdminController`, the server increments `CurrentKeyVersion` and sets `PendingKeyRotation = true` for remaining members.
* **HMAC Cryptographic Proof:** To execute a rotation, a client signs the update with an HMAC-SHA256 tag derived from the active PSK.
* **Complete Rogue Admin Protection:** Even if a server administrator steals a `deviceToken`, they cannot forge a valid key rotation because they lack the active PSK. Client devices reject any signature that fails local verification.

## Key Server Responsibilities

* **Zero-Knowledge Key Storage:** The .NET server never generates, reads, or stores the PSK in plain text or ciphertext form.
* **Group Key Versioning:** The server tracks a monotonic key version counter (`KeyVersion: int`) per group.
* **Payload Relay:** Stores and returns raw encrypted string payloads (`EncryptedPayload: string`) attached to their respective `KeyVersion`.
* **Roster Change Triggers:** Automatically flags group members to perform a key rotation whenever group membership changes.

---

## Database Schema Updates (EF Core / SQLite)

```csharp
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

**Group Membership Change Workflow (Join / Leave)**

When a user joins or leaves a group, the server updates the state to signal client-side key re-negotiation:

1. **Member Event Triggered:**
   * A user leaves, is removed via `AdminController`, or a new user is added.

2. **Server Updates Group Metadata:**
   * Increment `Group.CurrentKeyVersion += 1`.
   * Set `PendingKeyRotation = true` for all remaining valid group members.
   * If a user was removed, revoke their authorization tokens so they cannot fetch new encrypted payloads.

3. **Client Notification:**
   * The next time active clients check in via `GET /api/v1/location/status`, the server passes `CurrentKeyVersion` and `PendingKeyRotation = true`.

---

## Key Exchange & Rotation Protocol

Because the server cannot generate or hold the PSK, key exchange and key rotation rely on HMAC proofs of the current key:

```text
[ Active Client A ]             [ .NET Backend ]             [ Active Client B ]
        │                              │                              │
        │ ─── 1. Member Left/Added ──► │                              │
        │                              │ ◄── 2. Poll /location/status │
        │ ◄── 3. Flag: Rotation Req ── │     (PendingKeyRotation=true)│
        │                              │                              │
        │ ─── 4. POST /rotate-key ───► │                              │
        │     (Payload + HMAC Sig)     │ ─── 5. Relay Payload ──────► │
        │                              │                              │
        │                              │ ◄── 6. Client B Verifies ─── │
        │                              │      HMAC Signature          │
        │                              │      (Adopts New Key)        │
```

1. **Initial Key Exchange (Out-of-Band):**
   * **Direct Exchange:** When creating a group or adding the first devices, the PSK is established out-of-band via a direct mechanism (such as scanning a QR code containing the raw passphrase or using peer-to-peer ECDH key exchange).

2. **Rotation Phase (HMAC-Verified):**
   When a membership change occurs, remaining devices rotate the key securely using proof-of-possession of the old key:
   1. **Key Generation:** Client A generates a new passphrase locally using Web Crypto (`crypto.getRandomValues()`).
   2. **Payload Signing:** Client A constructs a rotation payload (`GroupId + NewKeyVersion + EncryptedNewKey`) and generates an HMAC-SHA256 signature using its current active PSK as the signing key.
   3. **Server Relay:** Client A sends the signed payload to `POST /api/v1/location/rotate-key`. The server verifies device authorization, updates `CurrentKeyVersion`, and relays the payload to remaining group members.
   4. **Signature Verification & Adoption:** When Client B receives the payload, it verifies the HMAC signature using its local copy of the current active PSK. If verification succeeds, Client B updates its stored PSK to the new key.

---

## Mitigation Against Server Admin Attacks

* **Attack Vector:** A malicious or compromised server admin attempts to use a stolen `deviceToken` to trigger an unauthorized key rotation and inject a key they control.
* **Mitigation:** The rogue admin possesses the `deviceToken` but does not possess the active PSK. Any key rotation request generated by the admin will lack a valid HMAC signature derived from the true active PSK.
* **Client Behavior:** Client applications verify the HMAC tag before accepting any new key. When the signature check fails (`verifyKeyRotationPayload` returns `false`), the client rejects the new key, logs a security alert, and retains its valid PSK.

---

## Implementation Checklist

* [x] **Data Model:** Updated EF Core entities (`Group`, `GroupMember`, `LocationUpdate`) to include `KeyVersion` and `PendingKeyRotation`.
* [x] **Crypto Service:** Added `signKeyRotationPayload` and `verifyKeyRotationPayload` using Web Crypto API HMAC-SHA256.
* [x] **Pinia Store:** Integrated `processPendingKeyRotation` and status checks into `src/stores/location.ts`.
* [x] **API Endpoints:** Added `POST /api/v1/location/rotate-key` and `GET /api/v1/location/status` in `LocationController.cs`.
* [x] **Admin Actions:** Updated `AdminController` to increment `CurrentKeyVersion` and set `PendingKeyRotation = true` on membership updates.
* [x] **Unit Tests:** Verified unit tests in `crypto.spec.ts` and `location.spec.ts`.

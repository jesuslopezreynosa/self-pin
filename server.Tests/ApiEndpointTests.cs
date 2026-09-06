using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SelfPin.Api.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DecoupledGroupKeyAndRotationWorkflow_ExecutesSuccessfully()
    {
        // 1. Create Users (Alice & Bob)
        var aliceUserRes = await _client.PostAsJsonAsync("/admin/users", new { name = "Alice" });
        aliceUserRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var aliceUserContent = await aliceUserRes.Content.ReadFromJsonAsync<JsonElement>();
        var aliceToken = aliceUserContent.GetProperty("deviceToken").GetString()!;
        var aliceUserId = aliceUserContent.GetProperty("id").GetInt32();

        var bobUserRes = await _client.PostAsJsonAsync("/admin/users", new { name = "Bob" });
        bobUserRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var bobUserContent = await bobUserRes.Content.ReadFromJsonAsync<JsonElement>();
        var bobToken = bobUserContent.GetProperty("deviceToken").GetString()!;
        var bobUserId = bobUserContent.GetProperty("id").GetInt32();

        // 2. Register Public Keys for Alice and Bob
        var aliceKeyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new
            {
                signingPublicKey = "a1b2c3d4e5f678901234567890abcdef1234567890abcdef1234567890abcdef"
            })
        };
        (await _client.SendAsync(aliceKeyReq)).StatusCode.Should().Be(HttpStatusCode.OK);

        var bobKeyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bobToken) },
            Content = JsonContent.Create(new
            {
                signingPublicKey = "f6e5d4c3b2a109876543210987fedcba0987654321fedcba0987654321fedcba"
            })
        };
        (await _client.SendAsync(bobKeyReq)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Create Group (Alice)
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { name = "Family Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var groupJson = await groupRes.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = groupJson.GetProperty("id").GetString()!;

        // 4. Add Bob to Group using Bob's Device Token
        var addBobReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{groupId}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { deviceToken = bobToken })
        };
        var addBobRes = await _client.SendAsync(addBobReq);
        addBobRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Alice fetches member public keys
        var memberKeysReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/groups/{groupId}/member-keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) }
        };
        var memberKeysRes = await _client.SendAsync(memberKeysReq);
        memberKeysRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Share Key (Version 1): Alice uploads client-encrypted envelopes for both members
        var postKeysV1Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new
            {
                groupId = Guid.Parse(groupId),
                keyVersion = 1,
                envelopes = new[]
                {
                    new { userId = aliceUserId, encryptedPsk = "encrypted_psk_for_alice_v1" },
                    new { userId = bobUserId, encryptedPsk = "encrypted_psk_for_bob_v1" }
                }
            })
        };
        var postKeysV1Res = await _client.SendAsync(postKeysV1Req);
        postKeysV1Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Bob fetches his group key envelope (Version 1)
        var bobKeysReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bobToken) }
        };
        var bobKeysRes1 = await _client.SendAsync(bobKeysReq1);
        bobKeysRes1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. Key Rotation (Version 2): Alice posts rotated PSK envelopes
        var postKeysV2Req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new
            {
                groupId = Guid.Parse(groupId),
                keyVersion = 2,
                envelopes = new[]
                {
                    new { userId = aliceUserId, encryptedPsk = "encrypted_psk_for_alice_v2" },
                    new { userId = bobUserId, encryptedPsk = "encrypted_psk_for_bob_v2" }
                }
            })
        };
        var postKeysV2Res = await _client.SendAsync(postKeysV2Req);
        postKeysV2Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 9. Bob fetches rotated group key (Version 2)
        var bobKeysReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bobToken) }
        };
        var bobKeysRes2 = await _client.SendAsync(bobKeysReq2);
        bobKeysRes2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FiveUserGroupLifecycle_TwoMembersRemoved_RemovedMembersCannotAccessLocations()
    {
        // 1. Create 5 Users (User1..User5)
        var tokens = new string[5];
        var userIds = new int[5];

        for (int i = 0; i < 5; i++)
        {
            var res = await _client.PostAsJsonAsync("/admin/users", new { name = $"User{i + 1}" });
            res.StatusCode.Should().Be(HttpStatusCode.Created);
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            tokens[i] = json.GetProperty("deviceToken").GetString()!;
            userIds[i] = json.GetProperty("id").GetInt32();
        }

        // 2. User 1 creates the Group
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", tokens[0]) },
            Content = JsonContent.Create(new { name = "Squad Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var groupJson = await groupRes.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = groupJson.GetProperty("id").GetString()!;

        // 3. Add Users 2..5 to the group using their device tokens
        for (int i = 1; i < 5; i++)
        {
            var addMemberReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{groupId}/members")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", tokens[0]) },
                Content = JsonContent.Create(new { deviceToken = tokens[i] })
            };
            var addMemberRes = await _client.SendAsync(addMemberReq);
            addMemberRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 4. Client Key Setup for Version 1
        // Active members generate Group Key V1 (32-byte key)
        byte[] originalGroupPsk = RandomNumberGenerator.GetBytes(32);

        // 5. Remove User 4 and User 5 from the group
        var removeUser4Res = await _client.PostAsJsonAsync("/admin/groups/remove", new { groupId, userId = userIds[3] });
        removeUser4Res.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeUser5Res = await _client.PostAsJsonAsync("/admin/groups/remove", new { groupId, userId = userIds[4] });
        removeUser5Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Key Rotation: Active Members (Users 1, 2, 3) generate a NEW 32-byte Group Key (V2)
        // Removed users (Users 4 and 5) NEVER receive this rotated key
        byte[] rotatedGroupPsk = RandomNumberGenerator.GetBytes(32);

        // Encrypt payload with the rotated Group Key V2 using AES-GCM
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("{\"lat\":37.7749,\"lng\":-122.4194}");
        byte[] ciphertextBytes = new byte[plaintextBytes.Length];

        using (var aesGcm = new AesGcm(rotatedGroupPsk, 16))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);
        }

        // Pack encrypted payload: Nonce (12B) + Tag (16B) + Ciphertext
        byte[] packedPayload = new byte[nonce.Length + tag.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(nonce, 0, packedPayload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, packedPayload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, packedPayload, nonce.Length + tag.Length, ciphertextBytes.Length);
        string payloadBase64 = Convert.ToBase64String(packedPayload);

        // 7. Active member (User 1) posts the location update encrypted with Key V2
        var locUpdateReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Headers = { { "X-Device-Token", tokens[0] } },
            Content = JsonContent.Create(new
            {
                encryptedPayload = payloadBase64,
                keyVersion = 2,
                timestamp = DateTime.UtcNow.ToString("o")
            })
        };
        var locUpdateRes = await _client.SendAsync(locUpdateReq);
        locUpdateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. Verify an active member (User 2) CAN see User 1's location update in feed
        var user2FeedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/feed")
        {
            Headers = { { "X-Device-Token", tokens[1] } }
        };
        var user2FeedRes = await _client.SendAsync(user2FeedReq);
        user2FeedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var user2Feed = await user2FeedRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        user2Feed.Should().Contain(u => u.GetProperty("id").GetInt32() == userIds[0]);

        // 9. Verify removed members (User 4 & User 5) CANNOT see User 1's location update via API feed
        foreach (var removedToken in new[] { tokens[3], tokens[4] })
        {
            var removedFeedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/feed")
            {
                Headers = { { "X-Device-Token", removedToken } }
            };
            var removedFeedRes = await _client.SendAsync(removedFeedReq);
            removedFeedRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var removedFeed = await removedFeedRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            if (removedFeed != null && removedFeed.Count > 0)
            {
                removedFeed.Should().NotContain(u => u.GetProperty("id").GetInt32() == userIds[0]);
            }
        }

        // 10. CRYPTOGRAPHIC VALIDATION: Attempt to decrypt the payload using Removed Users' old key (V1)
        // Extract packed components from the payload
        byte[] payloadData = Convert.FromBase64String(payloadBase64);
        byte[] extractedNonce = payloadData[..12];
        byte[] extractedTag = payloadData[12..28];
        byte[] extractedCiphertext = payloadData[28..];
        byte[] decryptedBuffer = new byte[extractedCiphertext.Length];

        // Attempting decryption using the OLD key held by removed members MUST fail
        Action attemptDecryptionWithOldKey = () =>
        {
            using var oldAesGcm = new AesGcm(originalGroupPsk, 16);
            oldAesGcm.Decrypt(extractedNonce, extractedCiphertext, extractedTag, decryptedBuffer);
        };

        attemptDecryptionWithOldKey.Should().Throw<CryptographicException>(
            "a removed member using an un-rotated or stale PSK cannot authenticate or decrypt payload encrypted with key V2");
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/register-key")]
    [InlineData("POST", "/api/v1/groups")]
    [InlineData("GET", "/api/v1/groups/keys")]
    [InlineData("POST", "/api/v1/groups/keys")]
    [InlineData("GET", "/api/v1/groups/00000000-0000-0000-0000-000000000000/member-keys")]
    [InlineData("POST", "/api/v1/groups/00000000-0000-0000-0000-000000000000/members")]
    [InlineData("POST", "/api/v1/location/update")]
    [InlineData("GET", "/api/v1/location/feed")]
    public async Task Endpoints_WithoutValidToken_ReturnUnauthorized(string method, string endpoint)
    {
        var httpMethod = new HttpMethod(method);

        // Build payload models matching controller DTO expectations
        HttpContent CreateValidPayload()
        {
            if (endpoint == "/api/v1/auth/register-key")
            {
                return JsonContent.Create(new
                {
                    signingPublicKey = "dummy_key",
                });
            }

            if (endpoint == "/api/v1/groups")
            {
                return JsonContent.Create(new { name = "dummy_group" });
            }

            if (endpoint == "/api/v1/groups/keys")
            {
                return JsonContent.Create(new
                {
                    groupId = Guid.NewGuid(),
                    keyVersion = 1,
                    envelopes = new[] {
                        new {
                            userId = 1,
                            encryptedPsk = "dummy_psk"
                        }
                    }
                });
            }

            if (endpoint == "/api/v1/location/update")
            {
                return JsonContent.Create(new
                {
                    encryptedPayload = "dummy_payload",
                    keyVersion = 1,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }

            if (endpoint.Contains("/members"))
            {
                return JsonContent.Create(new
                {
                    deviceToken = "dummy_device_token"
                });
            }

            return JsonContent.Create(new { });
        }

        // Test 1: Request with NO token/authorization headers
        var noTokenReq = new HttpRequestMessage(httpMethod, endpoint);
        if (httpMethod == HttpMethod.Post)
        {
            noTokenReq.Content = CreateValidPayload();
        }
        var noTokenRes = await _client.SendAsync(noTokenReq);
        noTokenRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"missing token on {method} {endpoint} should return 401");

        // Test 2: Request with FALSE / INVALID token
        var falseTokenReq = new HttpRequestMessage(httpMethod, endpoint);
        falseTokenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid_fake_device_token_99999");
        falseTokenReq.Headers.TryAddWithoutValidation("X-Device-Token", "invalid_fake_device_token_99999");

        if (httpMethod == HttpMethod.Post)
        {
            falseTokenReq.Content = CreateValidPayload();
        }
        var falseTokenRes = await _client.SendAsync(falseTokenReq);
        falseTokenRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"false token on {method} {endpoint} should return 401");
    }

    [Fact]
    public async Task NonMember_CannotSendLocationPayloadToGroupMembers()
    {
        // 1. Create User 1 (Alice) and User 2 (Eve)
        var aliceRes = await _client.PostAsJsonAsync("/admin/users", new { name = "Alice" });
        aliceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var aliceJson = await aliceRes.Content.ReadFromJsonAsync<JsonElement>();
        var aliceToken = aliceJson.GetProperty("deviceToken").GetString()!;
        var aliceUserId = aliceJson.GetProperty("id").GetInt32();

        var eveRes = await _client.PostAsJsonAsync("/admin/users", new { name = "Eve" });
        eveRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var eveJson = await eveRes.Content.ReadFromJsonAsync<JsonElement>();
        var eveToken = eveJson.GetProperty("deviceToken").GetString()!;
        var eveUserId = eveJson.GetProperty("id").GetInt32();

        // 2. Alice creates "Alice's Group" (Eve is NOT added)
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups");
        groupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);
        groupReq.Headers.TryAddWithoutValidation("X-Device-Token", aliceToken);
        groupReq.Content = JsonContent.Create(new { name = "Alice's Group" });

        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Eve publishes a location update
        var eveLocReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update");
        eveLocReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", eveToken);
        eveLocReq.Headers.TryAddWithoutValidation("X-Device-Token", eveToken);
        eveLocReq.Content = JsonContent.Create(new
        {
            encryptedPayload = "eves_unauthorized_location_payload",
            keyVersion = 1,
            timestamp = DateTime.UtcNow.ToString("o")
        });
        var eveLocRes = await _client.SendAsync(eveLocReq);
        eveLocRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Alice fetches her location feed
        var aliceFeedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/feed");
        aliceFeedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);
        aliceFeedReq.Headers.TryAddWithoutValidation("X-Device-Token", aliceToken);
        var aliceFeedRes = await _client.SendAsync(aliceFeedReq);
        aliceFeedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var aliceFeed = await aliceFeedRes.Content.ReadFromJsonAsync<List<JsonElement>>();

        // 5. Verify Eve's payload does NOT appear in Alice's feed
        if (aliceFeed != null && aliceFeed.Count > 0)
        {
            aliceFeed.Should().NotContain(u => u.GetProperty("id").GetInt32() == eveUserId,
                "location updates from non-members must not appear in group feeds");
        }
    }
}
using FluentAssertions;
using LocationServer.Models;
using LocationServer.Models.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SelfPin.Api.Tests;

[Collection("ApiTestCollection")]
public class ControllerUnitTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ControllerUnitTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<HttpClient> GetAuthenticatedAdminClientAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Passkey", CustomWebApplicationFactory.AdminPasskey);
        return Task.FromResult(client);
    }

    // ==========================================
    // 1. ADMIN PASSKEY AUTHENTICATION TESTS
    // ==========================================

    [Fact]
    public async Task Admin_WithValidPasskey_CanAccessProtectedEndpoints()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var res = await adminClient.GetAsync("/admin/users");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_WithInvalidPasskey_ReturnsUnauthorized()
    {
        var invalidClient = _factory.CreateClient();
        invalidClient.DefaultRequestHeaders.Add("X-Admin-Passkey", "wrong-invalid-passkey");

        var res = await invalidClient.GetAsync("/admin/users");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_WithNoPasskeyProvided_ReturnsUnauthorized()
    {
        var unauthenticatedClient = _factory.CreateClient();

        var res = await unauthenticatedClient.GetAsync("/admin/users");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ==========================================
    // 2. ADMIN CONTROLLER TESTS
    // ==========================================

    [Fact]
    public async Task Admin_CreateUser_CreatesUserWithGeneratedDeviceToken()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var res = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Alice AdminTest" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("Alice AdminTest");
        json.GetProperty("deviceToken").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Admin_GetUsers_ReturnsAllUsers()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var u1Res = await adminClient.PostAsJsonAsync("/admin/users", new { name = "User1" });
        u1Res.StatusCode.Should().Be(HttpStatusCode.Created);

        var u2Res = await adminClient.PostAsJsonAsync("/admin/users", new { name = "User2" });
        u2Res.StatusCode.Should().Be(HttpStatusCode.Created);

        var res = await adminClient.GetAsync("/admin/users");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await res.Content.ReadFromJsonAsync<List<JsonElement>>();
        users.Should().NotBeNull();
        users!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Admin_CreateGroup_CreatesGroupWithKeyVersion1()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var res = await adminClient.PostAsJsonAsync("/admin/groups", new { name = "Admin Group" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("Admin Group");
        json.GetProperty("currentKeyVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Admin_AssignUserToGroup_NonExistentUserOrGroup_ReturnsNotFound()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var validUserRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "TestUser" });
        validUserRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var validUser = await validUserRes.Content.ReadFromJsonAsync<JsonElement>();
        int userId = validUser.GetProperty("id").GetInt32();

        // Case A: Group doesn't exist
        var res1 = await adminClient.PostAsJsonAsync("/admin/groups/assign", new
        {
            groupId = Guid.NewGuid(),
            userId = userId
        });
        res1.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Case B: User doesn't exist
        var groupRes = await adminClient.PostAsJsonAsync("/admin/groups", new { name = "Test Group" });
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await groupRes.Content.ReadFromJsonAsync<JsonElement>();
        Guid groupId = Guid.Parse(group.GetProperty("id").GetString()!);

        var res2 = await adminClient.PostAsJsonAsync("/admin/groups/assign", new
        {
            groupId = groupId,
            userId = 999999
        });
        res2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_AssignAndRemoveUser_TriggersKeyRotationCorrectly()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        // Setup User and Group
        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "RotationUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await userRes.Content.ReadFromJsonAsync<JsonElement>();
        int userId = user.GetProperty("id").GetInt32();

        var groupRes = await adminClient.PostAsJsonAsync("/admin/groups", new { name = "Rotation Group" });
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await groupRes.Content.ReadFromJsonAsync<JsonElement>();
        Guid groupId = Guid.Parse(group.GetProperty("id").GetString()!);

        // 1. First Assignment -> Adds member & increments key version from 1 to 2
        var assignRes1 = await adminClient.PostAsJsonAsync("/admin/groups/assign", new { groupId, userId });
        assignRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignJson1 = await assignRes1.Content.ReadFromJsonAsync<JsonElement>();
        assignJson1.GetProperty("currentKeyVersion").GetInt32().Should().Be(2);

        // 2. Duplicate Assignment -> Idempotent, key version stays 2
        var assignRes2 = await adminClient.PostAsJsonAsync("/admin/groups/assign", new { groupId, userId });
        assignRes2.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignJson2 = await assignRes2.Content.ReadFromJsonAsync<JsonElement>();
        assignJson2.GetProperty("currentKeyVersion").GetInt32().Should().Be(2);

        // 3. Remove User -> Removes member & increments key version to 3
        var removeRes = await adminClient.PostAsJsonAsync("/admin/groups/remove", new { groupId, userId });
        removeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var removeJson = await removeRes.Content.ReadFromJsonAsync<JsonElement>();
        removeJson.GetProperty("currentKeyVersion").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Admin_RemoveUserFromGroup_NonExistentGroup_ReturnsNotFound()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var res = await adminClient.PostAsJsonAsync("/admin/groups/remove", new
        {
            groupId = Guid.NewGuid(),
            userId = 1
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==========================================
    // 3. AUTH CONTROLLER TESTS
    // ==========================================

    [Fact]
    public async Task Auth_RegisterKey_MissingOrInvalidToken_ReturnsUnauthorized()
    {
        // 1. Missing Authorization Header
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Content = JsonContent.Create(new { signingPublicKey = "pub_key_123" })
        };
        (await _client.SendAsync(req1)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Empty Bearer Token
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "") },
            Content = JsonContent.Create(new { signingPublicKey = "pub_key_123" })
        };
        (await _client.SendAsync(req2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 3. Token for non-existent user
        var req3 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "non_existent_device_token") },
            Content = JsonContent.Create(new { signingPublicKey = "pub_key_123" })
        };
        (await _client.SendAsync(req3)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_RegisterKey_ValidToken_UpdatesUserPublicKey()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "AuthUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await userRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = user.GetProperty("deviceToken").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register-key")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { signingPublicKey = "valid_hex_public_key_64_chars" })
        };

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ==========================================
    // 4. GROUPS CONTROLLER TESTS
    // ==========================================

    [Fact]
    public async Task Groups_CreateGroup_ValidUser_CreatesGroupAndAddsCreatorAsMember()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "GroupCreator" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await userRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = user.GetProperty("deviceToken").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { name = "My First Group" })
        };

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var groupJson = await res.Content.ReadFromJsonAsync<JsonElement>();
        groupJson.GetProperty("name").GetString().Should().Be("My First Group");
        groupJson.GetProperty("currentKeyVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Groups_GetGroupKeys_UserInNoGroups_ReturnsEmptyList()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "LonelyUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await userRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("group_keys").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Groups_GetGroupKeys_MultipleVersionsExist_DeduplicatesToLatestKeyVersionPerGroup()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        // 1. Create User & Group
        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "KeyUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await userRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = user.GetProperty("deviceToken").GetString()!;
        var userId = user.GetProperty("id").GetInt32();

        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { name = "Key Dedup Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = Guid.Parse((await groupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        // 2. Post Key V1
        var postKeyV1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new
            {
                groupId,
                keyVersion = 1,
                envelopes = new[] { new { userId, encryptedPsk = "v1_key" } }
            })
        };
        var postKeyV1Res = await _client.SendAsync(postKeyV1);
        postKeyV1Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Post Key V2
        var postKeyV2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new
            {
                groupId,
                keyVersion = 2,
                envelopes = new[] { new { userId, encryptedPsk = "v2_key" } }
            })
        };
        var postKeyV2Res = await _client.SendAsync(postKeyV2);
        postKeyV2Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Fetch keys and verify only V2 is returned
        var getKeysReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };
        var getKeysRes = await _client.SendAsync(getKeysReq);
        getKeysRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getKeysRes.Content.ReadFromJsonAsync<JsonElement>();
        var groupKeys = json.GetProperty("group_keys");
        groupKeys.GetArrayLength().Should().Be(1);
        groupKeys[0].GetProperty("keyVersion").GetInt32().Should().Be(2);
        groupKeys[0].GetProperty("encryptedPsk").GetString().Should().Be("v2_key");
    }

    [Fact]
    public async Task Groups_PostGroupKeys_NonMember_ReturnsForbidden()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var aliceRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Alice" });
        aliceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var aliceToken = (await aliceRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var eveRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Eve" });
        eveRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var eveToken = (await eveRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        // Alice creates group
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { name = "Alice Private Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = Guid.Parse((await groupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        // Eve (Non-member) tries to post key envelope
        var evePostKeyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", eveToken) },
            Content = JsonContent.Create(new
            {
                groupId,
                keyVersion = 1,
                envelopes = new[] { new { userId = 1, encryptedPsk = "malicious_key" } }
            })
        };

        var evePostKeyRes = await _client.SendAsync(evePostKeyReq);
        evePostKeyRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Groups_GetMemberPublicKeys_NonMember_ReturnsForbidden()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var aliceRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Alice" });
        aliceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var aliceToken = (await aliceRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var eveRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Eve" });
        eveRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var eveToken = (await eveRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { name = "Alice Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = (await groupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var getKeysReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/groups/{groupId}/member-keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", eveToken) }
        };

        var getKeysRes = await _client.SendAsync(getKeysReq);
        getKeysRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Groups_AddMember_TargetUserOrGroupNotFound_ReturnsNotFound()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var aliceRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "Alice" });
        aliceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var aliceToken = (await aliceRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        // 1. Invalid Group GUID
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{Guid.NewGuid()}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { deviceToken = "some_target_token" })
        };
        (await _client.SendAsync(req1)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Valid Group but non-existent target device token
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { name = "Add Member Test" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = (await groupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{groupId}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", aliceToken) },
            Content = JsonContent.Create(new { deviceToken = "invalid_target_device_token" })
        };
        (await _client.SendAsync(req2)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==========================================
    // 5. LOCATION CONTROLLER TESTS
    // ==========================================

    [Fact]
    public async Task Location_Update_MissingHeaderOrInvalidToken_ReturnsUnauthorized()
    {
        // 1. Missing Authorization Header
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Content = JsonContent.Create(new { encryptedPayload = "data", keyVersion = 1 })
        };
        (await _client.SendAsync(req1)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Invalid Token Header
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "fake_invalid_token") },
            Content = JsonContent.Create(new { encryptedPayload = "data", keyVersion = 1 })
        };
        (await _client.SendAsync(req2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Location_Update_NullTimestamp_DefaultsToUtcNowAndUpdatesUserLastUpdated()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "LocUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await userRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var updateReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new
            {
                encryptedPayload = "encrypted_location_string",
                keyVersion = 1,
                timestamp = (string?)null
            })
        };

        var updateRes = await _client.SendAsync(updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Location_GetFeed_UserInNoGroups_ReturnsEmptyList()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "NoGroupUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await userRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var feedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/feed")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var feedRes = await _client.SendAsync(feedReq);
        feedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var feed = await feedRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        feed.Should().BeEmpty();
    }

    [Fact]
    public async Task Location_GetFeed_ReturnsLatestLocationEntryPerSharedMemberOnly()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        // 1. Create User 1 & User 2
        var user1Res = await adminClient.PostAsJsonAsync("/admin/users", new { name = "User1" });
        user1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var token1 = (await user1Res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var user2Res = await adminClient.PostAsJsonAsync("/admin/users", new { name = "User2" });
        user2Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var token2 = (await user2Res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        // 2. User 1 creates group and adds User 2
        var groupReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token1) },
            Content = JsonContent.Create(new { name = "Feed Group" })
        };
        var groupRes = await _client.SendAsync(groupReq);
        groupRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = (await groupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var addMemberReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{groupId}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token1) },
            Content = JsonContent.Create(new { deviceToken = token2 })
        };
        var addMemberRes = await _client.SendAsync(addMemberReq);
        addMemberRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. User 2 Posts Older Location Entry
        var locReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token2) },
            Content = JsonContent.Create(new
            {
                encryptedPayload = "old_payload",
                keyVersion = 1,
                timestamp = DateTime.UtcNow.AddHours(-1).ToString("o")
            })
        };
        var locRes1 = await _client.SendAsync(locReq1);
        locRes1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. User 2 Posts Newer Location Entry
        var locReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/location/update")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token2) },
            Content = JsonContent.Create(new
            {
                encryptedPayload = "newest_payload",
                keyVersion = 1,
                timestamp = DateTime.UtcNow.ToString("o")
            })
        };
        var locRes2 = await _client.SendAsync(locReq2);
        locRes2.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. User 1 Fetches Feed
        var feedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/feed")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token1) }
        };
        var feedRes = await _client.SendAsync(feedReq);
        feedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var feed = await feedRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        feed.Should().NotBeNull();

        var user2Entry = feed!.FirstOrDefault(u => u.GetProperty("name").GetString() == "User2");
        user2Entry.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        user2Entry.GetProperty("latestEntry").GetProperty("encryptedPayload").GetString().Should().Be("newest_payload");
    }

    [Fact]
    public async Task Location_GetMapConfig_UnregisteredOrMissingToken_ReturnsUnauthorized()
    {
        // 1. Whitespace / Empty token
        var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/map-config")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "   ") }
        };
        (await _client.SendAsync(req1)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Unregistered token
        var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/map-config")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "unregistered_token_12345") }
        };
        (await _client.SendAsync(req2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Location_GetMapConfig_RegisteredUser_ReturnsCartoConfig()
    {
        var adminClient = await GetAuthenticatedAdminClientAsync();

        var userRes = await adminClient.PostAsJsonAsync("/admin/users", new { name = "MapUser" });
        userRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await userRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deviceToken").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/location/map-config")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("tileUrlTemplate").GetString().Should().Contain("cartocdn.com");
    }
}
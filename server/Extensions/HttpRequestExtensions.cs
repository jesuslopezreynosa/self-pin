namespace LocationServer.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>
    /// Extracts the Bearer token from the standard Authorization header.
    /// </summary>
    public static string? GetBearerToken(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            return null;

        var authHeader = authHeaderValues.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
            return null;

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        return authHeader.Trim();
    }
}
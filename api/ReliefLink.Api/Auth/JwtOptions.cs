namespace ReliefLink.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key. Must come from configuration or an environment variable, never from source control.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ReliefLink";
    public string Audience { get; set; } = "ReliefLinkClients";
    public int ExpiryMinutes { get; set; } = 60;
}

using System.Text.Json.Serialization;

namespace OAuthMaui;

/// <summary>
/// The user information that the ASP.NET Core sample returns when the application redeems a relay code.
/// </summary>
public sealed class BackEndUser
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

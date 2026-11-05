using System.Text.Json.Serialization;

namespace OAuthSamples
{
    /// <summary>
    /// The user that the ASP.NET Core sample returns when an application redeems the code of a relayed sign-in.
    /// </summary>
    public sealed class OAuthBackEndUser
    {
        /// <summary>Gets or sets the user ID at the provider.</summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        /// <summary>Gets or sets the user name.</summary>
        [JsonPropertyName("userName")]
        public string? UserName { get; set; }

        /// <summary>Gets or sets the email address.</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}

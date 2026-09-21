using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads the JSON that the web packages write themselves: the pending sign-in cookie and the relay entry.
    /// </summary>
    /// <remarks>Both web packages compile this file.</remarks>
    internal static class JsonElementExtensions
    {
        /// <summary>
        /// Gets a property whose value is a JSON string.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="propertyName">The property name, matched case-sensitively.</param>
        /// <returns>The string, or null when the property is missing or its value is not a JSON string.</returns>
        public static string? GetStringProperty(this JsonElement obj, string propertyName)
        {
            return obj.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}

using System.Text.Json;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Reads fields from the JSON objects that OAuth2 providers return.
    /// </summary>
    internal static class OAuth2Json
    {
        /// <summary>
        /// Parses text that must contain a JSON object.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The parsed document, which the caller disposes.</returns>
        /// <exception cref="JsonException">The text is not valid JSON, or its root value is not an object.</exception>
        public static JsonDocument ParseObject(string json)
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new JsonException("The response is not a JSON object.");
            }
            return document;
        }

        /// <summary>
        /// Gets a field of a JSON object as text.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="propertyName">The field name, matched case-sensitively.</param>
        /// <returns>
        /// Null when the field is missing or its value is JSON null; the value itself for a string; otherwise the JSON
        /// text of the value, so a numeric identifier keeps its digits.
        /// </returns>
        public static string? GetString(JsonElement obj, string propertyName)
        {
            if (!obj.TryGetProperty(propertyName, out var value))
                return null;

            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.String:
                    return value.GetString();
                default:
                    return value.GetRawText();
            }
        }
    }
}

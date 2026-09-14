using System.Globalization;
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
        /// Gets a field of a JSON object as text. Use it for user information, where providers differ in how they write values.
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

        /// <summary>
        /// Gets a protocol field whose value the specification defines as a string, such as <c>access_token</c> or <c>error</c>.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="propertyName">The field name, matched case-sensitively.</param>
        /// <returns>The string, or null when the field is missing or its value is not a JSON string.</returns>
        public static string? GetProtocolString(JsonElement obj, string propertyName)
        {
            return obj.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>
        /// Gets a field that holds a number of seconds, such as <c>expires_in</c>. Some providers write the number as a string.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="propertyName">The field name, matched case-sensitively.</param>
        /// <returns>
        /// The duration, or null when the field is missing or is not a whole number of seconds between 0 and
        /// <see cref="int.MaxValue"/>.
        /// </returns>
        public static TimeSpan? GetSeconds(JsonElement obj, string propertyName)
        {
            if (!obj.TryGetProperty(propertyName, out var value))
                return null;

            long seconds;
            if (value.ValueKind == JsonValueKind.Number)
            {
                if (!value.TryGetInt64(out seconds))
                    return null;
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                if (!long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out seconds))
                    return null;
            }
            else
            {
                return null;
            }

            return seconds >= 0 && seconds <= int.MaxValue ? TimeSpan.FromSeconds(seconds) : null;
        }
    }
}

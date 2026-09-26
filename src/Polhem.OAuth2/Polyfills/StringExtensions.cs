namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <c>string.Contains(char)</c>, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets, where the
    /// instance method of <see cref="string"/> takes precedence.
    /// </remarks>
    internal static class StringExtensions
    {
        /// <summary>
        /// Checks whether a string contains a character.
        /// </summary>
        /// <param name="value">The string to search.</param>
        /// <param name="c">The character to find.</param>
        /// <returns>True if the character occurs in the string.</returns>
        public static bool Contains(this string value, char c)
        {
            return value.IndexOf(c) >= 0;
        }
    }
}

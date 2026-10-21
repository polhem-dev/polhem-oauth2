namespace Polhem.OAuth2
{
    /// <summary>
    /// Compares Microsoft Entra ID client IDs, which are GUIDs, by value, so that a client ID configured in upper case
    /// matches the lower-case form that Entra ID writes in its tokens. A value that is not a GUID is compared ordinally.
    /// </summary>
    internal sealed class EntraClientIdComparer : IEqualityComparer<string>
    {
        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static EntraClientIdComparer Instance { get; } = new EntraClientIdComparer();

        private EntraClientIdComparer()
        {
        }

        /// <inheritdoc/>
        public bool Equals(string? x, string? y)
        {
            if (Guid.TryParse(x, out var left) && Guid.TryParse(y, out var right))
                return left == right;
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public int GetHashCode(string obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return Guid.TryParse(obj, out var value) ? value.GetHashCode() : StringComparer.Ordinal.GetHashCode(obj);
        }
    }
}

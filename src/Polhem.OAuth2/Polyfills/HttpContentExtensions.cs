namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds the <see cref="HttpContent"/> overloads that take a cancellation token, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets, where the
    /// instance methods of <see cref="HttpContent"/> take precedence. The read itself cannot be canceled on netstandard2.0,
    /// so the token is only checked before it starts.
    /// </remarks>
    internal static class HttpContentExtensions
    {
        /// <summary>
        /// Reads the content as a string.
        /// </summary>
        /// <param name="content">The content to read.</param>
        /// <param name="cancellationToken">Checked before the read starts.</param>
        /// <returns>The content as a string.</returns>
        public static Task<string> ReadAsStringAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsStringAsync();
        }
    }
}

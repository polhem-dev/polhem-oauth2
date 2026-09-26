namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <c>CancellationTokenSource.CancelAsync</c>, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets. This version
    /// runs the callbacks of the token synchronously, as <see cref="CancellationTokenSource.Cancel()"/> does.
    /// </remarks>
    internal static class CancellationTokenSourceExtensions
    {
        /// <summary>
        /// Cancels the token of a source.
        /// </summary>
        /// <param name="source">The source to cancel.</param>
        /// <returns>A task that has completed, because the callbacks have run by the time it returns.</returns>
        public static Task CancelAsync(this CancellationTokenSource source)
        {
            source.Cancel();
            return Task.CompletedTask;
        }
    }
}

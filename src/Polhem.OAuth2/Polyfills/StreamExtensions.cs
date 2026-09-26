namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds a <see cref="Stream"/> write of a whole buffer with a cancellation token, which the other targets have as
    /// <c>WriteAsync(ReadOnlyMemory&lt;byte&gt;, CancellationToken)</c>.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets. There a byte
    /// array converts to <c>ReadOnlyMemory&lt;byte&gt;</c>, so the same call reaches the memory overload.
    /// </remarks>
    internal static class StreamExtensions
    {
        /// <summary>
        /// Writes a whole buffer to a stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="buffer">The bytes to write.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the bytes are written.</returns>
        public static Task WriteAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
    }
}

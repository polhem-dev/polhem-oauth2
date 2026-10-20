namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <see cref="Stream"/> reads and writes of a whole buffer with a cancellation token, which the other targets have as
    /// <c>ReadAsync(Memory&lt;byte&gt;, CancellationToken)</c> and <c>WriteAsync(ReadOnlyMemory&lt;byte&gt;, CancellationToken)</c>.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets. There a byte
    /// array converts to <c>Memory&lt;byte&gt;</c> and <c>ReadOnlyMemory&lt;byte&gt;</c>, so the same call reaches the memory overload.
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

        /// <summary>
        /// Writes a range of memory to a stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="buffer">The bytes to write, which must be backed by an array, as <c>AsMemory</c> of an array is.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the bytes are written.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is not backed by an array.</exception>
        public static Task WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var segment))
                throw new ArgumentException("The memory is not backed by an array.", nameof(buffer));
            return stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
        }

        /// <summary>
        /// Reads from a stream into a whole buffer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>The number of bytes read, or 0 at the end of the stream.</returns>
        public static Task<int> ReadAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            return stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        }
    }
}

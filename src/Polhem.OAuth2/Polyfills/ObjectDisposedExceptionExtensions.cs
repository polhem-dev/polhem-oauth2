namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <c>ObjectDisposedException.ThrowIf</c>, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets, where the
    /// method is part of <see cref="ObjectDisposedException"/>.
    /// </remarks>
    internal static class ObjectDisposedExceptionExtensions
    {
        extension(ObjectDisposedException)
        {
            /// <summary>
            /// Throws an <see cref="ObjectDisposedException"/> if a condition is true.
            /// </summary>
            /// <param name="condition">True if the instance has been disposed.</param>
            /// <param name="instance">The disposed instance, whose type names the object in the exception.</param>
            /// <exception cref="ObjectDisposedException"><paramref name="condition"/> is true.</exception>
            public static void ThrowIf(bool condition, object instance)
            {
                if (condition)
                    throw new ObjectDisposedException(instance?.GetType().FullName);
            }
        }
    }
}

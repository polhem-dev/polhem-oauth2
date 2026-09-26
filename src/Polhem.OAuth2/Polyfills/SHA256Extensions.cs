using System.Security.Cryptography;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <c>SHA256.HashData</c>, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets, where the
    /// method is part of <see cref="SHA256"/>.
    /// </remarks>
    internal static class SHA256Extensions
    {
        extension(SHA256)
        {
            /// <summary>
            /// Computes the SHA-256 hash of data.
            /// </summary>
            /// <param name="source">The data to hash.</param>
            /// <returns>The hash.</returns>
            public static byte[] HashData(byte[] source)
            {
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(source);
                }
            }
        }
    }
}

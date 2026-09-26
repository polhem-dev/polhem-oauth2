using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Polhem.OAuth2
{
    /// <summary>
    /// Adds <c>ArgumentNullException.ThrowIfNull</c>, which netstandard2.0 does not have.
    /// </summary>
    /// <remarks>
    /// The netstandard2.0 build compiles this class, and the project file excludes it from the other targets, where the
    /// method is part of <see cref="ArgumentNullException"/>. Callers write the same code for every target.
    /// </remarks>
    internal static class ArgumentNullExceptionExtensions
    {
        extension(ArgumentNullException)
        {
            /// <summary>
            /// Throws an <see cref="ArgumentNullException"/> if an argument is null.
            /// </summary>
            /// <param name="argument">The argument to check.</param>
            /// <param name="paramName">The name of the parameter, which the compiler fills in.</param>
            /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
            public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                if (argument is null)
                    throw new ArgumentNullException(paramName);
            }
        }
    }
}

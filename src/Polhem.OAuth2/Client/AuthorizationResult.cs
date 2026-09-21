using System.Diagnostics.CodeAnalysis;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The outcome of a sign-in: the provider name, tokens and user information when it succeeded, or the exception that
    /// made it fail.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core has a type of the same name in <c>Microsoft.AspNetCore.Authorization</c>. A file that imports both
    /// namespaces and names the type gets error CS0104: declare the variable with <c>var</c>, or add
    /// <c>using AuthorizationResult = Polhem.OAuth2.AuthorizationResult;</c>.
    /// </remarks>
    public sealed class AuthorizationResult
    {
        private AuthorizationResult(bool isSuccess, string? providerName, TokenResponse? token, UserInfo? userInfo, Exception? exception)
        {
            IsSuccess = isSuccess;
            ProviderName = providerName;
            Token = token;
            UserInfo = userInfo;
            Exception = exception;
        }

        /// <summary>
        /// Gets a value indicating whether the sign-in succeeded. When it is <see langword="true"/>, <see cref="ProviderName"/>,
        /// <see cref="Token"/> and <see cref="UserInfo"/> are set; otherwise <see cref="Exception"/> is set.
        /// </summary>
        [MemberNotNullWhen(true, nameof(ProviderName), nameof(Token), nameof(UserInfo))]
        [MemberNotNullWhen(false, nameof(Exception))]
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the provider name, or null for a failed result.
        /// </summary>
        public string? ProviderName { get; }

        /// <summary>
        /// Gets the tokens returned by the token endpoint, or null for a failed result.
        /// </summary>
        public TokenResponse? Token { get; }

        /// <summary>
        /// Gets the user information, or null for a failed result.
        /// </summary>
        public UserInfo? UserInfo { get; }

        /// <summary>
        /// Gets the exception that made the sign-in fail, or null for a successful result.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="token">The tokens returned by the token endpoint.</param>
        /// <param name="userInfo">The user information.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="providerName"/>, <paramref name="token"/> or <paramref name="userInfo"/> is null.</exception>
        public static AuthorizationResult Success(string providerName, TokenResponse token, UserInfo userInfo)
        {
            if (providerName is null)
                throw new ArgumentNullException(nameof(providerName));
            if (token is null)
                throw new ArgumentNullException(nameof(token));
            if (userInfo is null)
                throw new ArgumentNullException(nameof(userInfo));

            return new AuthorizationResult(true, providerName, token, userInfo, null);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="exception">The exception that made the sign-in fail.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
        public static AuthorizationResult Failure(Exception exception)
        {
            if (exception is null)
                throw new ArgumentNullException(nameof(exception));

            return new AuthorizationResult(false, null, null, null, exception);
        }
    }
}

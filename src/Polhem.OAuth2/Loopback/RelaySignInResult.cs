using System.Diagnostics.CodeAnalysis;

namespace Polhem.OAuth2
{
    /// <summary>
    /// The result of <see cref="LoopbackRelayClient.SignInAsync"/>: the single-use code that the back end returned, with the
    /// code verifier to redeem it, or the exception of a sign-in that did not return a code.
    /// </summary>
    public sealed class RelaySignInResult
    {
        private RelaySignInResult(bool isSuccess, string? code, string? codeVerifier, Exception? exception)
        {
            IsSuccess = isSuccess;
            Code = code;
            CodeVerifier = codeVerifier;
            Exception = exception;
        }

        /// <summary>
        /// Gets a value indicating whether the back end returned a code.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Code), nameof(CodeVerifier))]
        [MemberNotNullWhen(false, nameof(Exception))]
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the single-use code that the back end returned, or null when the sign-in failed.
        /// </summary>
        /// <remarks>
        /// Send it to the back end together with <see cref="CodeVerifier"/>, which redeems them for the user, for example with
        /// <c>OAuth2Manager.RedeemAppCodeAsync</c> of Polhem.OAuth2.AspNetCore.
        /// </remarks>
        public string? Code { get; }

        /// <summary>
        /// Gets the PKCE code verifier whose challenge started the sign-in, or null when the sign-in failed. Only this
        /// application holds it, so a code that another program intercepts cannot be redeemed.
        /// </summary>
        public string? CodeVerifier { get; }

        /// <summary>
        /// Gets the exception of a failed sign-in, or null when it succeeded. An <see cref="OAuth2Exception"/> whose
        /// <see cref="OAuth2Exception.Error"/> is set carries the error that the back end returned.
        /// </summary>
        public Exception? Exception { get; }

        internal static RelaySignInResult Success(string code, string codeVerifier)
        {
            return new RelaySignInResult(true, code, codeVerifier, null);
        }

        internal static RelaySignInResult Failure(Exception exception)
        {
            return new RelaySignInResult(false, null, null, exception);
        }
    }
}

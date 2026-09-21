using Microsoft.AspNetCore.Mvc;
using Polhem.OAuth2.AspNetCore;

namespace OAuthAspNetCore.Controllers
{
    public class AuthController : Controller
    {
        private readonly OAuth2Manager _oauth2Manager;

        public AuthController(OAuth2Manager oauth2Manager)
        {
            _oauth2Manager = oauth2Manager;
        }

        [HttpGet("/auth/login/{clientName=Google}")]
        public IActionResult Login(string clientName)
        {
            return Redirect(_oauth2Manager.CreateAuthorizationUrl(HttpContext, clientName));
        }

        // The OAuthMaui sample opens this URL in WebAuthenticator to sign in through this application (ADR-006).
        [HttpGet("/auth/app/{clientName}")]
        public IActionResult AppLogin(string clientName, [FromQuery(Name = "redirect_uri")] string redirectUri, [FromQuery(Name = "code_challenge")] string codeChallenge)
        {
            // Anyone can open this URL with any values, so a value that is not valid is answered with 400, not thrown.
            return _oauth2Manager.TryRedirectToAppAuthorization(HttpContext, clientName, redirectUri, codeChallenge)
                ? new EmptyResult()
                : BadRequest("The client, the redirect URI or the code challenge is not valid.");
        }

        [HttpGet("/auth/callback")]
        public async Task<IActionResult> Callback()
        {
            var result = await _oauth2Manager.CompleteAuthorizationAsync(HttpContext, HttpContext.RequestAborted);

            // A sign-in started by the OAuthMaui sample goes back to the application with a single-use code.
            if (await _oauth2Manager.RedirectToAppAsync(HttpContext, result, HttpContext.RequestAborted))
                return new EmptyResult();

            if (result.IsSuccess)
            {
                return Content($"ProviderName: {result.ProviderName}\n" +
                               $"UserID: {result.UserInfo.UserId}\n" +
                               $"UserName: {result.UserInfo.UserName}\n" +
                               $"Email: {result.UserInfo.Email}\n" +
                               $"RawJson: {result.UserInfo.RawJson}");
            }

            return Content($"Error: {result.Exception.Message}");
        }

        // The OAuthMaui sample posts the code it received and its code verifier here. A real back end would issue its own
        // session for the user at this point; the sample returns the user information so the application can show it.
        [HttpPost("/auth/app/redeem")]
        public async Task<IActionResult> AppRedeem([FromForm(Name = "client")] string clientName, [FromForm] string code, [FromForm(Name = "code_verifier")] string codeVerifier)
        {
            var user = await _oauth2Manager.RedeemAppCodeAsync(clientName ?? string.Empty, code ?? string.Empty, codeVerifier ?? string.Empty, HttpContext.RequestAborted);
            if (user is null)
                return BadRequest("The code is not valid.");

            return Json(new { provider = clientName, userId = user.UserId, userName = user.UserName, email = user.Email });
        }
    }
}

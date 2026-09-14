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

        [HttpGet("/auth/login")]
        public IActionResult Login()
        {
            return Redirect(_oauth2Manager.CreateAuthorizationUrl(HttpContext, "Google"));
        }

        [HttpGet("/auth/callback")]
        public async Task<IActionResult> Callback()
        {
            var result = await _oauth2Manager.CompleteAuthorizationAsync(HttpContext, HttpContext.RequestAborted);
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
    }
}

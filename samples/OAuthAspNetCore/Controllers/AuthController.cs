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
            _oauth2Manager.RedirectToAuthorization("Google");
            return new EmptyResult();
        }

        [HttpGet("/auth/callback")]
        public async Task<IActionResult> Callback()
        {
            var result = await _oauth2Manager.ValidateAuthorization();
            if (result.IsSuccess && result.UserInfo is { } user)
            {
                return Content($"ProviderName: {result.ProviderName}\n" +
                               $"UserID: {user.UserId}\n" +
                               $"UserName: {user.UserName}\n" +
                               $"Email: {user.Email}\n" +
                               $"RawJson: {user.RawJson}");
            }

            return Content($"Error: {result.Exception?.Message}");
        }
    }
}

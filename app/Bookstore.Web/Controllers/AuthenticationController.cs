using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BobsBookstoreClassic.Data;
using System;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        public ActionResult Login(string? redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");

            return Redirect(redirectUri);
        }

        [AllowAnonymous]
        public ActionResult LogOut()
        {
            return BookstoreConfiguration.GetSetting("Services/Authentication") == "aws" ? CognitoSignOut() : LocalSignOut();
        }

        private ActionResult LocalSignOut()
        {
            if (Request.Cookies["LocalAuthentication"] != null)
            {
                Response.Cookies.Append("LocalAuthentication", string.Empty, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(-1)
                });
            }

            return RedirectToAction("Index", "Home");
        }

        private ActionResult CognitoSignOut()
        {
            if (Request.Cookies[".AspNet.Cookies"] != null)
            {
                Response.Cookies.Append(".AspNet.Cookies", string.Empty, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(-1)
                });
            }

            var domain = BookstoreConfiguration.GetSetting("Authentication/Cognito/CognitoDomain");
            var clientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}

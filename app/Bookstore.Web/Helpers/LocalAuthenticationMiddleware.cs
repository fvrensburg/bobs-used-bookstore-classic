using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bookstore.Web.Helpers
{
    /// <summary>
    /// ASP.NET Core middleware that provides local (non-Cognito) authentication for development.
    /// It simulates a logged-in user without requiring an external identity provider.
    /// </summary>
    public class LocalAuthenticationMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private const string LocalAuthCookieName = "LocalAuthentication";

        private readonly RequestDelegate _next;

        public LocalAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICustomerService customerService)
        {
            if (context.Request.Path.Value?.StartsWith("/Authentication/Login", StringComparison.OrdinalIgnoreCase) == true)
            {
                CreateClaimsPrincipal(context);
                await SaveCustomerDetailsAsync(context, customerService);

                context.Response.Cookies.Append(LocalAuthCookieName, "1", new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(1),
                    Path = "/"
                });

                context.Response.Redirect("/");
                return;
            }
            else if (context.Request.Cookies[LocalAuthCookieName] != null)
            {
                CreateClaimsPrincipal(context);
                await SaveCustomerDetailsAsync(context, customerService);
            }

            await _next(context);
        }

        private static void CreateClaimsPrincipal(HttpContext context)
        {
            var identity = new ClaimsIdentity("Application");
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

            context.User = new ClaimsPrincipal(identity);
        }

        private static async Task SaveCustomerDetailsAsync(HttpContext context, ICustomerService customerService)
        {
            if (context.User?.Identity is not ClaimsIdentity identity) return;

            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value ?? UserId,
                identity.Name ?? "bookstoreuser",
                identity.FindFirst("given_name")?.Value ?? "Bookstore",
                identity.FindFirst("family_name")?.Value ?? "User");

            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}

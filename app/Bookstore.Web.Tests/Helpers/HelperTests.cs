using Bookstore.Domain.Customers;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using System.Security.Principal;
using Xunit;

namespace Bookstore.Web.Tests.Helpers;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetSub_ReturnsNameidentifierClaim()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("nameidentifier", "user-123"));
        var principal = new ClaimsPrincipal(identity);

        var sub = principal.GetSub();

        Assert.Equal("user-123", sub);
    }

    [Fact]
    public void GetSub_ReturnsNull_WhenNoNameidentifierClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var sub = principal.GetSub();

        Assert.Null(sub);
    }

    [Fact]
    public void GetSub_OnIdentity_ReturnsNameidentifierClaim()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("nameidentifier", "user-abc"));

        var sub = identity.GetSub();

        Assert.Equal("user-abc", sub);
    }
}

public class HttpContextExtensionsTests
{
    [Fact]
    public void GetShoppingCartCorrelationId_ReturnsExistingCookieValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "ShoppingCartId=existing-cart-id";

        var id = context.GetShoppingCartCorrelationId();

        Assert.Equal("existing-cart-id", id);
    }

    [Fact]
    public void GetShoppingCartCorrelationId_GeneratesNewGuid_WhenNoCookie()
    {
        var context = new DefaultHttpContext();

        var id = context.GetShoppingCartCorrelationId();

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(Guid.TryParse(id, out _), "Should be a valid GUID when unauthenticated with no cookie");
    }

    [Fact]
    public void GetShoppingCartCorrelationId_UsesSubFromAuthenticatedUser()
    {
        const string expectedSub = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity("Application");
        identity.AddClaim(new Claim("nameidentifier", expectedSub));
        context.User = new ClaimsPrincipal(identity);

        var id = context.GetShoppingCartCorrelationId();

        Assert.Equal(expectedSub, id);
    }
}

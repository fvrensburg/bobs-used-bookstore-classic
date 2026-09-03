using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace Bookstore.Web.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="LocalAuthenticationMiddleware"/>.
/// These tests verify that the middleware sets ClaimsPrincipal, writes the
/// LocalAuthentication cookie, and redirects correctly on login.
/// </summary>
public class LocalAuthenticationMiddlewareTests
{
    private const string LocalAuthCookie = "LocalAuthentication";

    private static DefaultHttpContext BuildContext(string? path = null, string? cookieHeader = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream(); // avoid "cannot write to response" exceptions
        if (path is not null) ctx.Request.Path = path;
        if (cookieHeader is not null) ctx.Request.Headers["Cookie"] = cookieHeader;
        return ctx;
    }

    [Fact]
    public async Task Invoke_LoginPath_SetsClaimsPrincipalAndRedirects()
    {
        bool nextCalled = false;
        var middleware = new LocalAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var ctx = BuildContext("/Authentication/Login");
        var customerService = new FakeCustomerService();

        await middleware.InvokeAsync(ctx, customerService);

        // Redirected to / — next should NOT have been called
        Assert.False(nextCalled);
        Assert.Equal(302, ctx.Response.StatusCode);
        Assert.Equal("/", ctx.Response.Headers.Location.ToString());

        // ClaimsPrincipal is set
        Assert.NotNull(ctx.User);
        Assert.True(ctx.User.Identity?.IsAuthenticated);
        Assert.Equal("bookstoreuser", ctx.User.Identity?.Name);
    }

    [Fact]
    public async Task Invoke_ExistingCookie_SetsClaimsPrincipalAndCallsNext()
    {
        bool nextCalled = false;
        var middleware = new LocalAuthenticationMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var ctx = BuildContext("/Home/Index", $"{LocalAuthCookie}=1");
        var customerService = new FakeCustomerService();

        await middleware.InvokeAsync(ctx, customerService);

        Assert.True(nextCalled, "next() should be called for authenticated non-login requests");
        Assert.NotNull(ctx.User);
        Assert.True(ctx.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Invoke_NoCookie_NonLoginPath_CallsNextWithoutAuthenticating()
    {
        bool nextCalled = false;
        var middleware = new LocalAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var ctx = BuildContext("/Home/Index");
        var customerService = new FakeCustomerService();

        await middleware.InvokeAsync(ctx, customerService);

        Assert.True(nextCalled);
        // User should be the anonymous default (not authenticated)
        Assert.False(ctx.User?.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task Invoke_LoginPath_CustomerServiceCalled()
    {
        var customerService = new FakeCustomerService();
        var middleware = new LocalAuthenticationMiddleware(_ => Task.CompletedTask);
        var ctx = BuildContext("/Authentication/Login");

        await middleware.InvokeAsync(ctx, customerService);

        Assert.Equal(1, customerService.CreateOrUpdateCallCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private sealed class FakeCustomerService : ICustomerService
    {
        public int CreateOrUpdateCallCount { get; private set; }

        public Task CreateOrUpdateCustomerAsync(CreateOrUpdateCustomerDto dto)
        {
            CreateOrUpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<Domain.Customers.Customer?> GetAsync(int id) =>
            Task.FromResult<Domain.Customers.Customer?>(null);

        public Task<Domain.Customers.Customer?> GetAsync(string sub) =>
            Task.FromResult<Domain.Customers.Customer?>(null);
    }
}

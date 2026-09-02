using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Bookstore.Web.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CheckoutService"/>.
    ///
    /// The key migration concern addressed here is the IHttpContextAccessor setup.
    /// In the legacy .NET Framework codebase, the shopping-cart cookie was accessed
    /// through System.Web.HttpContext.Current, which cannot be used in .NET 8 unit
    /// tests. The correct approach is to:
    ///   1. Inject IHttpContextAccessor into the service.
    ///   2. In tests, create a Microsoft.AspNetCore.Http.DefaultHttpContext, set the
    ///      Cookie request header, and return it from the mocked IHttpContextAccessor.
    /// </summary>
    public class CheckoutServiceTests
    {
        /// <summary>
        /// Verifies that CalculateTaxAsync returns the correct tax amount when the
        /// shopping cart contains items.
        ///
        /// Test setup notes:
        /// - IHttpContextAccessor is mocked with a DefaultHttpContext whose Request
        ///   carries a ShoppingCartId cookie. This is the ASP.NET Core equivalent of
        ///   the old System.Web.HttpContext.Current cookie approach.
        /// - IShoppingCartService is mocked to return a pre-populated cart without
        ///   hitting a real database.
        /// - IConfiguration is built in-memory with Tax:Rate = 0.10.
        /// </summary>
        [Fact]
        public async Task CalculatesTax()
        {
            // Arrange -------------------------------------------------------
            const string cartCorrelationId = "test-cart-id";
            const decimal bookPrice = 100m;
            const decimal expectedTax = 10.00m; // 10% of $100

            // Set up HttpContext with a ShoppingCartId cookie using
            // Microsoft.AspNetCore.Http.DefaultHttpContext — NOT System.Web.HttpContext.
            // DefaultHttpContext.Request.Cookies reads from the raw Cookie header.
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers[HeaderNames.Cookie] =
                $"ShoppingCartId={cartCorrelationId}";

            var mockAccessor = new Mock<IHttpContextAccessor>();
            mockAccessor.Setup(a => a.HttpContext).Returns(httpContext);

            // Build a ShoppingCart with one $100 item (in-stock, WantToBuy = true).
            // Book and ShoppingCartItem have public property setters so we can wire
            // the navigation properties after construction without EF Core.
            var book = new Book(
                name: "Test Book",
                author: "Test Author",
                ISBN: "123-456",
                publisherId: 1,
                bookTypeId: 1,
                genreId: 1,
                conditionId: 1,
                price: bookPrice,
                quantity: 5);

            var cart = new ShoppingCart(cartCorrelationId);
            var item = new ShoppingCartItem(cart, bookId: book.Id, quantity: 1, wantToBuy: true);
            item.Book = book;
            cart.ShoppingCartItems.Add(item);

            var mockCartService = new Mock<IShoppingCartService>();
            mockCartService
                .Setup(s => s.GetShoppingCartAsync(cartCorrelationId))
                .ReturnsAsync(cart);

            // IConfiguration with Tax:Rate = 0.10 — reads from appsettings.json in
            // production; supplied in-memory here so the test is self-contained.
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tax:Rate"] = "0.10"
                })
                .Build();

            var service = new CheckoutService(
                mockAccessor.Object,
                mockCartService.Object,
                configuration);

            // Act -----------------------------------------------------------
            var tax = await service.CalculateTaxAsync();

            // Assert --------------------------------------------------------
            Assert.Equal(expectedTax, tax);

            // The service must have resolved the cart ID from the cookie, not
            // generated a random GUID — confirmed by the mock verification below.
            mockCartService.Verify(
                s => s.GetShoppingCartAsync(cartCorrelationId),
                Times.Once);
        }

        /// <summary>
        /// Verifies that CalculateTaxAsync returns 0 when the cart is null
        /// (e.g., first visit with no cart yet created in the database).
        /// </summary>
        [Fact]
        public async Task CalculatesTax_ReturnsZero_WhenCartIsNull()
        {
            // Arrange
            const string cartCorrelationId = "empty-cart-id";

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers[HeaderNames.Cookie] =
                $"ShoppingCartId={cartCorrelationId}";

            var mockAccessor = new Mock<IHttpContextAccessor>();
            mockAccessor.Setup(a => a.HttpContext).Returns(httpContext);

            var mockCartService = new Mock<IShoppingCartService>();
            mockCartService
                .Setup(s => s.GetShoppingCartAsync(cartCorrelationId))
                .ReturnsAsync((ShoppingCart?)null);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tax:Rate"] = "0.10"
                })
                .Build();

            var service = new CheckoutService(
                mockAccessor.Object,
                mockCartService.Object,
                configuration);

            // Act
            var tax = await service.CalculateTaxAsync();

            // Assert
            Assert.Equal(0m, tax);
        }

        /// <summary>
        /// Verifies that the tax rate from IConfiguration is applied correctly
        /// when a non-default rate is configured (e.g., 20%).
        /// </summary>
        [Fact]
        public async Task CalculatesTax_UsesConfiguredTaxRate()
        {
            // Arrange
            const string cartCorrelationId = "rate-test-cart";
            const decimal bookPrice = 50m;
            const decimal taxRate = 0.20m;   // 20% tax
            const decimal expectedTax = 10m; // 20% of $50

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers[HeaderNames.Cookie] =
                $"ShoppingCartId={cartCorrelationId}";

            var mockAccessor = new Mock<IHttpContextAccessor>();
            mockAccessor.Setup(a => a.HttpContext).Returns(httpContext);

            var book = new Book(
                name: "Another Book",
                author: "Author",
                ISBN: "789-012",
                publisherId: 1,
                bookTypeId: 1,
                genreId: 1,
                conditionId: 1,
                price: bookPrice,
                quantity: 3);

            var cart = new ShoppingCart(cartCorrelationId);
            var item = new ShoppingCartItem(cart, bookId: book.Id, quantity: 1, wantToBuy: true);
            item.Book = book;
            cart.ShoppingCartItems.Add(item);

            var mockCartService = new Mock<IShoppingCartService>();
            mockCartService
                .Setup(s => s.GetShoppingCartAsync(cartCorrelationId))
                .ReturnsAsync(cart);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tax:Rate"] = taxRate.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })
                .Build();

            var service = new CheckoutService(
                mockAccessor.Object,
                mockCartService.Object,
                configuration);

            // Act
            var tax = await service.CalculateTaxAsync();

            // Assert
            Assert.Equal(expectedTax, tax);
        }
    }
}

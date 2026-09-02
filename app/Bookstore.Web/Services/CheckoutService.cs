using Bookstore.Domain.Carts;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Bookstore.Web.Services
{
    public interface ICheckoutService
    {
        /// <summary>
        /// Calculates the tax for the current user's shopping cart.
        /// The tax rate is read from configuration key "Tax:Rate" (default 10%).
        /// The shopping cart is identified by the ShoppingCartId cookie on the
        /// current HTTP request, resolved via IHttpContextAccessor.
        /// </summary>
        Task<decimal> CalculateTaxAsync();
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IConfiguration _configuration;

        public CheckoutService(
            IHttpContextAccessor httpContextAccessor,
            IShoppingCartService shoppingCartService,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _shoppingCartService = shoppingCartService;
            _configuration = configuration;
        }

        public async Task<decimal> CalculateTaxAsync()
        {
            // GetShoppingCartCorrelationId reads the ShoppingCartId cookie from
            // HttpContext.Request (Microsoft.AspNetCore.Http.HttpContext — NOT
            // System.Web.HttpContext) and writes the value back as a response cookie.
            var correlationId = _httpContextAccessor.HttpContext.GetShoppingCartCorrelationId();

            var cart = await _shoppingCartService.GetShoppingCartAsync(correlationId);

            if (cart == null) return 0m;

            var taxRate = _configuration.GetValue<decimal>("Tax:Rate", 0.1m);
            var subTotal = cart.GetSubTotal(ShoppingCartItemFilter.IncludeOutOfStockItems);

            return Math.Round(subTotal * taxRate, 2);
        }
    }
}

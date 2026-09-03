using Bookstore.Domain.Orders;
using System.Collections.Generic;
using System.Linq;

namespace Bookstore.Web.ViewModel.Checkout
{
    public class CheckoutFinishedViewModel
    {
        public IEnumerable<CheckoutFinishedItemViewModel> Items { get; set; } = new List<CheckoutFinishedItemViewModel>();

        public CheckoutFinishedViewModel(Order? order)
        {
            if (order == null) return;

            Items = order.OrderItems.Select(x => new CheckoutFinishedItemViewModel
            {
                BookId = x.Book?.Id ?? 0,
                Bookname = x.Book?.Name,
                Price = x.Book?.Price ?? 0m,
                Quantity = x.Quantity,
                Url = x.Book?.CoverImageUrl
            });
        }
    }

    public class CheckoutFinishedItemViewModel
    {
        public string? Bookname { get; set; }

        public long BookId { get; set; }

        public int Quantity { get; set; }

        public string? Url { get; set; }

        public decimal Price { get; set; }
    }
}

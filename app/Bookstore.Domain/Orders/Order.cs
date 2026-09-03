using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Customers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bookstore.Domain.Orders
{
    public class Order : Entity
    {
#pragma warning disable CS8618 // EF Core requires a parameterless constructor; nav properties are populated by EF.
        protected Order() { }
#pragma warning restore CS8618

        public Order(int customerId, int addressId)
        {
            CustomerId = customerId;
            AddressId = addressId;
        }

        private readonly List<OrderItem> orderItems = new List<OrderItem>();

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int AddressId { get; set; }
        public Address? Address { get; set; }

        public ICollection<OrderItem> OrderItems => orderItems;

        public DateTime DeliveryDate { get; set; } = DateTime.Now.AddDays(7);

        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

        public decimal Tax => SubTotal * 0.1m;

        public decimal SubTotal => OrderItems.Sum(x => x.Book?.Price ?? 0m);

        public decimal Total => SubTotal + Tax;

        public void AddOrderItem(Book book, int quantity)
        {
            orderItems.Add(new OrderItem(this, book, quantity));
        }
    }
}
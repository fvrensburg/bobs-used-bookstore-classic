using Microsoft.EntityFrameworkCore;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;

namespace Bookstore.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Address> Address { get; set; }

        public DbSet<Book> Book { get; set; }

        public DbSet<Customer> Customer { get; set; }

        public DbSet<Order> Order { get; set; }

        public DbSet<ShoppingCart> ShoppingCart { get; set; }

        public DbSet<OrderItem> OrderItem { get; set; }

        public DbSet<Offer> Offer { get; set; }

        public DbSet<ReferenceDataItem> ReferenceData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Table name mappings (using DbSet property names, no pluralization needed)
            modelBuilder.Entity<ReferenceDataItem>().ToTable("ReferenceData");

            // Customer
            modelBuilder.Entity<Customer>().Property(x => x.Sub).HasMaxLength(450);
            modelBuilder.Entity<Customer>().HasIndex(x => x.Sub).IsUnique();

            // Book relationships – disable cascade delete to avoid multiple cascade path issues
            modelBuilder.Entity<Book>().HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>().HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>().HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Book>().HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            // Offer relationships – disable cascade delete
            modelBuilder.Entity<Offer>().HasOne(x => x.Publisher).WithMany().HasForeignKey(x => x.PublisherId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>().HasOne(x => x.BookType).WithMany().HasForeignKey(x => x.BookTypeId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>().HasOne(x => x.Genre).WithMany().HasForeignKey(x => x.GenreId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
            modelBuilder.Entity<Offer>().HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            // Order – disable cascade delete on Customer
            modelBuilder.Entity<Order>().HasOne(x => x.Customer).WithMany().OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            // ShoppingCartItem – composite key
            modelBuilder.Entity<ShoppingCartItem>().HasKey(x => new { x.Id, x.ShoppingCartId });
            modelBuilder.Entity<ShoppingCartItem>().Property(x => x.Id).ValueGeneratedOnAdd();
        }
    }
}

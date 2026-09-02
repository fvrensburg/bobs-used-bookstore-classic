using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Address> Address { get; set; } = null!;

        public DbSet<Book> Book { get; set; } = null!;

        public DbSet<Customer> Customer { get; set; } = null!;

        public DbSet<Order> Order { get; set; } = null!;

        public DbSet<ShoppingCart> ShoppingCart { get; set; } = null!;

        public DbSet<OrderItem> OrderItem { get; set; } = null!;

        public DbSet<Offer> Offer { get; set; } = null!;

        public DbSet<ReferenceDataItem> ReferenceData { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Remove pluralization — table names match entity names
            // EF Core does not pluralize by default, so we explicitly set table names
            modelBuilder.Entity<Address>().ToTable("Address");
            modelBuilder.Entity<Book>().ToTable("Book");
            modelBuilder.Entity<Customer>().ToTable("Customer");
            modelBuilder.Entity<Order>().ToTable("Order");
            modelBuilder.Entity<ShoppingCart>().ToTable("ShoppingCart");
            modelBuilder.Entity<OrderItem>().ToTable("OrderItem");
            modelBuilder.Entity<Offer>().ToTable("Offer");

            modelBuilder.Entity<Customer>()
                .Property(x => x.Sub)
                .HasColumnType("nvarchar")
                .HasMaxLength(450);

            modelBuilder.Entity<Customer>()
                .HasIndex(x => x.Sub)
                .IsUnique();

            modelBuilder.Entity<Book>()
                .HasOne(x => x.Publisher).WithMany()
                .HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasOne(x => x.BookType).WithMany()
                .HasForeignKey(x => x.BookTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasOne(x => x.Genre).WithMany()
                .HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasOne(x => x.Condition).WithMany()
                .HasForeignKey(x => x.ConditionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Publisher).WithMany()
                .HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.BookType).WithMany()
                .HasForeignKey(x => x.BookTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Genre).WithMany()
                .HasForeignKey(x => x.GenreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Offer>()
                .HasOne(x => x.Condition).WithMany()
                .HasForeignKey(x => x.ConditionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(x => x.Customer).WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            // ReferenceData table name to match the modern version
            modelBuilder.Entity<ReferenceDataItem>().ToTable("ReferenceData");

            // ShoppingCartItem composite key
            modelBuilder.Entity<ShoppingCartItem>()
                .HasKey(x => new { x.Id, x.ShoppingCartId });

            modelBuilder.Entity<ShoppingCartItem>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Seed initial data
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReferenceDataItem>().HasData(
                new ReferenceDataItem(ReferenceDataType.BookType, "Hardcover") { Id = 1 },
                new ReferenceDataItem(ReferenceDataType.BookType, "Trade Paperback") { Id = 2 },
                new ReferenceDataItem(ReferenceDataType.BookType, "Mass Market Paperback") { Id = 3 },

                new ReferenceDataItem(ReferenceDataType.Condition, "New") { Id = 4 },
                new ReferenceDataItem(ReferenceDataType.Condition, "Like New") { Id = 5 },
                new ReferenceDataItem(ReferenceDataType.Condition, "Good") { Id = 6 },
                new ReferenceDataItem(ReferenceDataType.Condition, "Acceptable") { Id = 7 },

                new ReferenceDataItem(ReferenceDataType.Genre, "Biographies") { Id = 8 },
                new ReferenceDataItem(ReferenceDataType.Genre, "Children's Books") { Id = 9 },
                new ReferenceDataItem(ReferenceDataType.Genre, "History") { Id = 10 },
                new ReferenceDataItem(ReferenceDataType.Genre, "Literature & Fiction") { Id = 11 },
                new ReferenceDataItem(ReferenceDataType.Genre, "Mystery, Thriller & Suspense") { Id = 12 },
                new ReferenceDataItem(ReferenceDataType.Genre, "Science Fiction & Fantasy") { Id = 13 },
                new ReferenceDataItem(ReferenceDataType.Genre, "Travel") { Id = 14 },

                new ReferenceDataItem(ReferenceDataType.Publisher, "Arcadia Books") { Id = 15 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Astral Publishing") { Id = 16 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Moonlight Publishing") { Id = 17 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Dreamscape Press") { Id = 18 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Enchanted Library") { Id = 19 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Fantasia House") { Id = 20 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Horizon Books") { Id = 21 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Infinity Press") { Id = 22 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Paradigm Publishing") { Id = 23 },
                new ReferenceDataItem(ReferenceDataType.Publisher, "Aurora Publishing") { Id = 24 }
            );
        }
    }
}

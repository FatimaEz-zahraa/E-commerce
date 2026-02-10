using E_commerce.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace E_commerce.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<UserSearchHistory> UserSearchHistory { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var listComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
            );

            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredBrands)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null)!
                )
                .Metadata.SetValueComparer(listComparer);

            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredCategories)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null)!
                )
                .Metadata.SetValueComparer(listComparer);

            // Collection avec ValueComparer
            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredBrands)
                .HasConversion(
                    v => string.Join(',', v),           // To store as CSV
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                )
                .Metadata.SetValueComparer(
                    new ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    )
                );

            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredCategories)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                )
                .Metadata.SetValueComparer(
                    new ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    )
                );

            // Decimal precision
            builder.Entity<ApplicationUser>()
                .Property(u => u.AverageBudget)
                .HasPrecision(18, 2); // Ajuste selon tes besoins

            builder.Entity<ApplicationUser>()
                .Property(u => u.TotalAmountSpent)
                .HasPrecision(18, 2);

            // Configuration pour les propriétés de liste en JSON
            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredCategories)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());

            builder.Entity<ApplicationUser>()
                .Property(u => u.PreferredBrands)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());

            // Configuration de l'adresse comme propriété détenue (Owned Entity)
            builder.Entity<ApplicationUser>()
                .OwnsOne(u => u.ShippingAddress, sa =>
                {
                    sa.Property(a => a.Street).HasMaxLength(200);
                    sa.Property(a => a.City).HasMaxLength(100);
                    sa.Property(a => a.PostalCode).HasMaxLength(20);
                    sa.Property(a => a.Country).HasMaxLength(100);
                    sa.Property(a => a.State).HasMaxLength(100);
                    sa.Property(a => a.Apartment).HasMaxLength(100);
                    sa.Property(a => a.Landmark).HasMaxLength(200);
                    sa.Property(a => a.Label).HasMaxLength(100);
                    sa.Property(a => a.PhoneNumber).HasMaxLength(50);
                    sa.Property(a => a.RecipientName).HasMaxLength(150);
                });

            builder.Entity<ApplicationUser>()
                .OwnsOne(u => u.BillingAddress, ba =>
                {
                    ba.Property(a => a.Street).HasMaxLength(200);
                    ba.Property(a => a.City).HasMaxLength(100);
                    ba.Property(a => a.PostalCode).HasMaxLength(20);
                    ba.Property(a => a.Country).HasMaxLength(100);
                    ba.Property(a => a.State).HasMaxLength(100);
                    ba.Property(a => a.Apartment).HasMaxLength(100);
                    ba.Property(a => a.Landmark).HasMaxLength(200);
                    ba.Property(a => a.Label).HasMaxLength(100);
                    ba.Property(a => a.PhoneNumber).HasMaxLength(50);
                    ba.Property(a => a.RecipientName).HasMaxLength(150);
                });

            // Configuration de Product
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Description).IsRequired();
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.Category).HasMaxLength(100);
                entity.Property(p => p.Brand).HasMaxLength(100);
                entity.Property(p => p.ImageUrl).HasMaxLength(500);
                entity.Property(p => p.Rating).HasPrecision(3, 2);
                entity.Property(p => p.StockQuantity).IsRequired();
            });

            // Configuration de Cart
            builder.Entity<Cart>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.UserId).IsRequired();
                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuration de CartItem
            builder.Entity<CartItem>(entity =>
            {
                entity.HasKey(ci => ci.Id);
                entity.Property(ci => ci.Quantity).IsRequired();

                entity.HasOne(ci => ci.Cart)
                    .WithMany(c => c.Items)
                    .HasForeignKey(ci => ci.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ci => ci.Product)
                    .WithMany()
                    .HasForeignKey(ci => ci.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration de Review
            builder.Entity<Review>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Rating).IsRequired();
                entity.Property(r => r.Comment).HasMaxLength(1000);

                entity.HasOne(r => r.Product)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(r => r.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration de Order (si la classe existe)
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(o => o.Subtotal).HasPrecision(18, 2);
                entity.Property(o => o.ShippingCost).HasPrecision(18, 2);
                entity.Property(o => o.Tax).HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
                entity.Property(o => o.PaymentMethod).HasMaxLength(50);
                entity.Property(o => o.ShippingMethod).HasMaxLength(100);
                entity.Property(o => o.TrackingNumber).HasMaxLength(100);
                entity.Property(o => o.Notes).HasMaxLength(500);

                // Adresse de livraison comme propriété détenue
                entity.OwnsOne(o => o.ShippingAddress, sa =>
                {
                    sa.Property(a => a.Street).HasMaxLength(200);
                    sa.Property(a => a.City).HasMaxLength(100);
                    sa.Property(a => a.PostalCode).HasMaxLength(20);
                    sa.Property(a => a.Country).HasMaxLength(100);
                    sa.Property(a => a.State).HasMaxLength(100);
                    sa.Property(a => a.Apartment).HasMaxLength(100);
                    sa.Property(a => a.Landmark).HasMaxLength(200);
                    sa.Property(a => a.Label).HasMaxLength(100);
                    sa.Property(a => a.PhoneNumber).HasMaxLength(50);
                    sa.Property(a => a.RecipientName).HasMaxLength(150);
                });

                entity.HasOne(o => o.User)
                    .WithMany(u => u.Orders)
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration de OrderItem (si la classe existe)
            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);
                entity.Property(oi => oi.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
                entity.Property(oi => oi.TotalPrice).HasPrecision(18, 2);
                entity.Property(oi => oi.ProductCategory).HasMaxLength(100);
                entity.Property(oi => oi.ProductBrand).HasMaxLength(100);
                entity.Property(oi => oi.ProductRating).HasPrecision(3, 2);

                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.Product)
                    .WithMany()
                    .HasForeignKey(oi => oi.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration de WishlistItem (si la classe existe)
            builder.Entity<WishlistItem>(entity =>
            {
                entity.HasKey(wi => wi.Id);
                entity.Property(wi => wi.Notes).HasMaxLength(500);

                entity.HasOne(wi => wi.User)
                    .WithMany(u => u.Wishlist)
                    .HasForeignKey(wi => wi.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(wi => wi.Product)
                    .WithMany()
                    .HasForeignKey(wi => wi.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuration de UserSearchHistory (si la classe existe)
            builder.Entity<UserSearchHistory>(entity =>
            {
                entity.HasKey(ush => ush.Id);
                entity.Property(ush => ush.SearchQuery).IsRequired().HasMaxLength(200);
                entity.Property(ush => ush.Category).HasMaxLength(100);

                entity.HasOne(ush => ush.User)
                    .WithMany(u => u.SearchHistory)
                    .HasForeignKey(ush => ush.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuration de NotificationPreference (si la classe existe)
            builder.Entity<NotificationPreference>(entity =>
            {
                entity.HasKey(np => np.Id);
                entity.Property(np => np.PreferredTime).HasMaxLength(10);

                entity.HasOne(np => np.User)
                    .WithMany(u => u.NotificationPreferences)
                    .HasForeignKey(np => np.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Indexes pour les performances
            builder.Entity<Product>()
                .HasIndex(p => p.Category);

            builder.Entity<Product>()
                .HasIndex(p => p.Brand);

            builder.Entity<Product>()
                .HasIndex(p => p.Rating);

            builder.Entity<Review>()
                .HasIndex(r => new { r.ProductId, r.CreatedAt });

            builder.Entity<Review>()
                .HasIndex(r => r.Rating);

            builder.Entity<Cart>()
                .HasIndex(c => c.UserId);

            builder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId });

            builder.Entity<Order>()
                .HasIndex(o => new { o.UserId, o.OrderDate });

            builder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            builder.Entity<WishlistItem>()
                .HasIndex(wi => new { wi.UserId, wi.ProductId })
                .IsUnique();

            builder.Entity<UserSearchHistory>()
                .HasIndex(ush => new { ush.UserId, ush.SearchedAt });

            builder.Entity<NotificationPreference>()
                .HasIndex(np => new { np.UserId, np.Type })
                .IsUnique();
        }
    }
}
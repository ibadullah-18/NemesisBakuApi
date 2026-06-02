using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<Size> Sizes { get; set; }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }

    public DbSet<BasketItem> BasketItems { get; set; }
    public DbSet<Favorite> Favorites { get; set; }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public DbSet<UserOtpCode> UserOtpCodes { get; set; }
    public DbSet<WhatsAppMessageLog> WhatsAppMessageLogs { get; set; }
    public DbSet<WhatsAppClickLog> WhatsAppClickLogs { get; set; }
    public DbSet<WhatsAppProductInquiry> WhatsAppProductInquiries { get; set; }

    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<PromoCode> PromoCodes { get; set; }
    public DbSet<PromoCodeUsage> PromoCodeUsages { get; set; }

    public DbSet<UserActivityLog> UserActivityLogs { get; set; }
    public DbSet<SiteVisit> SiteVisits { get; set; }
    public DbSet<StoreInfo> StoreInfos { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>()
            .HasIndex(x => x.ProductCode)
            .IsUnique();

        builder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Product>()
            .Property(x => x.DiscountPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(x => x.DeliveryPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(x => x.TotalProductPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(x => x.PromoDiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(x => x.TotalPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<OrderItem>()
            .Property(x => x.UnitPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<OrderItem>()
            .Property(x => x.TotalPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<PromoCode>()
            .Property(x => x.DiscountValue)
            .HasColumnType("decimal(18,2)");

        builder.Entity<PromoCode>()
            .Property(x => x.MinOrderAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<PromoCodeUsage>()
            .Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<StoreInfo>()
            .Property(x => x.Latitude)
            .HasColumnType("decimal(18,8)");

        builder.Entity<StoreInfo>()
            .Property(x => x.Longitude)
            .HasColumnType("decimal(18,8)");

        builder.Entity<Order>()
            .Property(x => x.Latitude)
            .HasColumnType("decimal(18,8)");

        builder.Entity<Order>()
            .Property(x => x.Longitude)
            .HasColumnType("decimal(18,8)");

        builder.Entity<BasketItem>()
            .HasIndex(x => new { x.UserId, x.ProductVariantId })
            .IsUnique();

        builder.Entity<Favorite>()
            .HasIndex(x => new { x.UserId, x.ProductId })
            .IsUnique();

        builder.Entity<ProductVariant>()
            .HasIndex(x => new { x.ProductId, x.SizeId, x.ColorId })
            .IsUnique();

        builder.Entity<PromoCode>()
            .HasIndex(x => x.Code)
            .IsUnique();

        builder.Entity<Order>()
            .HasIndex(x => x.OrderNumber)
            .IsUnique();

        builder.Entity<RefreshToken>()
            .HasIndex(x => x.Token)
            .IsUnique();

        builder.Entity<Product>()
            .HasMany(x => x.BasketItems)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Product>()
            .HasMany(x => x.Variants)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<BasketItem>()
            .HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Category>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Brand>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Product>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<ProductImage>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<ProductVariant>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<BasketItem>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Favorite>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Order>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<OrderItem>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Campaign>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<PromoCode>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<StoreInfo>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<RefreshToken>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<AuditLog>()
            .HasQueryFilter(x => !x.IsDeleted);
    }
}
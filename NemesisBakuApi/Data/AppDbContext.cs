using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Data;

public class AppDbContext
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
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

    public DbSet<PromoCode> PromoCodes { get; set; }
    public DbSet<PromoCodeUsage> PromoCodeUsages { get; set; }

    public DbSet<UserActivityLog> UserActivityLogs { get; set; }
    public DbSet<SiteVisit> SiteVisits { get; set; }
    public DbSet<StoreInfo> StoreInfos { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<UserAddress> UserAddresses { get; set; }

    public DbSet<PromoPage> PromoPages { get; set; }
    public DbSet<PromoPageProduct> PromoPageProducts { get; set; }

    public DbSet<HomeSection> HomeSections { get; set; }
    public DbSet<HomeSectionProduct> HomeSectionProducts { get; set; }

    public DbSet<EmailAnnouncement> EmailAnnouncements { get; set; }

    public DbSet<BasketLowStockEmailLog> BasketLowStockEmailLogs
    { get; set; }

    public DbSet<CourierPhone> CourierPhones { get; set; }

    public DbSet<TelegramOrderNotification> TelegramOrderNotifications
    { get; set; }

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureDecimalProperties(builder);
        ConfigureConcurrency(builder);
        ConfigureIndexes(builder);
        ConfigureRelationships(builder);
        ConfigureQueryFilters(builder);
    }

    private static void ConfigureDecimalProperties(
        ModelBuilder builder)
    {
        builder.Entity<Product>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Entity<Product>()
            .Property(x => x.DiscountPrice)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.DeliveryPrice)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.DeliveryDistanceKm)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.TotalProductPrice)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.PromoDiscountAmount)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        builder.Entity<Order>()
            .Property(x => x.Latitude)
            .HasPrecision(18, 8);

        builder.Entity<Order>()
            .Property(x => x.Longitude)
            .HasPrecision(18, 8);

        builder.Entity<OrderItem>()
            .Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        builder.Entity<OrderItem>()
            .Property(x => x.TotalPrice)
            .HasPrecision(18, 2);

        builder.Entity<PromoCode>()
            .Property(x => x.DiscountValue)
            .HasPrecision(18, 2);

        builder.Entity<PromoCode>()
            .Property(x => x.MinOrderAmount)
            .HasPrecision(18, 2);

        builder.Entity<PromoCodeUsage>()
            .Property(x => x.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Entity<StoreInfo>()
            .Property(x => x.Latitude)
            .HasPrecision(18, 8);

        builder.Entity<StoreInfo>()
            .Property(x => x.Longitude)
            .HasPrecision(18, 8);

        builder.Entity<UserAddress>()
            .Property(x => x.Latitude)
            .HasPrecision(18, 8);

        builder.Entity<UserAddress>()
            .Property(x => x.Longitude)
            .HasPrecision(18, 8);
    }

    private static void ConfigureConcurrency(
        ModelBuilder builder)
    {
        builder.Entity<Order>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Entity<ProductVariant>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Entity<PromoCode>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Entity<RefreshToken>()
            .Property(x => x.RowVersion)
            .IsRowVersion();
    }

    private static void ConfigureIndexes(
        ModelBuilder builder)
    {
        builder.Entity<BasketItem>()
            .HasIndex(x => new
            {
                x.UserId,
                x.ProductVariantId
            })
            .IsUnique();

        builder.Entity<Favorite>()
            .HasIndex(x => new
            {
                x.UserId,
                x.ProductId
            })
            .IsUnique();

        builder.Entity<ProductVariant>()
            .HasIndex(x => new
            {
                x.ProductId,
                x.SizeId,
                x.ColorId
            })
            .IsUnique();

        builder.Entity<Product>()
            .HasIndex(x => new
            {
                x.IsActive,
                x.CreatedAt
            });

        builder.Entity<Product>()
            .HasIndex(x => new
            {
                x.IsActive,
                x.ViewCount
            });

        builder.Entity<Product>()
            .HasIndex(x => new
            {
                x.CategoryId,
                x.IsActive
            });

        builder.Entity<Product>()
            .HasIndex(x => new
            {
                x.BrandId,
                x.IsActive
            });

        builder.Entity<ProductVariant>()
            .HasIndex(x => new
            {
                x.ProductId,
                x.IsActive,
                x.StockCount
            });

        builder.Entity<Order>()
            .HasIndex(x => new
            {
                x.UserId,
                x.CreatedAt
            });

        builder.Entity<Order>()
            .HasIndex(x => new
            {
                x.Status,
                x.CreatedAt
            });

        builder.Entity<Order>()
            .HasIndex(x => x.OrderNumber)
            .IsUnique();

        builder.Entity<OrderStatusHistory>()
            .HasIndex(x => new
            {
                x.OrderId,
                x.CreatedAt
            });

        builder.Entity<PromoCode>()
            .HasIndex(x => x.Code)
            .IsUnique();

        builder.Entity<PromoCodeUsage>()
            .HasIndex(x => new
            {
                x.PromoCodeId,
                x.CreatedAt
            });

        builder.Entity<PromoCodeUsage>()
            .HasIndex(x => new
            {
                x.UserId,
                x.CreatedAt
            });

        builder.Entity<WhatsAppProductInquiry>()
            .HasIndex(x => new
            {
                x.ProductId,
                x.CreatedAt
            });

        builder.Entity<SiteVisit>()
            .HasIndex(x => x.VisitorId);

        builder.Entity<SiteVisit>()
            .HasIndex(x => x.VisitedAt);

        builder.Entity<SiteVisit>()
            .HasIndex(x => new
            {
                x.VisitorId,
                x.VisitedAt
            });

        builder.Entity<WhatsAppClickLog>()
            .HasIndex(x => new
            {
                x.ClickType,
                x.CreatedAt
            });

        builder.Entity<UserOtpCode>()
            .HasIndex(x => new
            {
                x.Email,
                x.Purpose,
                x.IsUsed,
                x.ExpiresAt
            });

        builder.Entity<RefreshToken>()
            .HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Entity<HomeSectionProduct>()
            .HasIndex(x => new
            {
                x.HomeSectionId,
                x.ProductId
            })
            .IsUnique();

        builder.Entity<BasketLowStockEmailLog>()
            .HasIndex(x => new
            {
                x.UserId,
                x.ProductVariantId
            })
            .IsUnique();

        builder.Entity<AppUser>()
            .HasIndex(x => x.TelegramChatId)
            .IsUnique()
            .HasFilter("[TelegramChatId] IS NOT NULL");

        builder.Entity<TelegramOrderNotification>()
            .HasIndex(x => new
            {
                x.OrderId,
                x.AdminUserId
            })
            .IsUnique();

        builder.Entity<TelegramOrderNotification>()
            .HasIndex(x => new
            {
                x.SentAt,
                x.NextAttemptAt,
                x.AttemptCount
            });
    }

    private static void ConfigureRelationships(
        ModelBuilder builder)
    {
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

        builder.Entity<OrderStatusHistory>()
            .HasOne(x => x.Order)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<OrderStatusHistory>()
            .HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PromoCodeUsage>()
            .HasOne(x => x.PromoCode)
            .WithMany(x => x.Usages)
            .HasForeignKey(x => x.PromoCodeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PromoCodeUsage>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PromoCodeUsage>()
            .HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<WhatsAppProductInquiry>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<WhatsAppProductInquiry>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PromoPage>()
            .HasMany(x => x.Products)
            .WithOne(x => x.PromoPage)
            .HasForeignKey(x => x.PromoPageId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PromoPageProduct>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<HomeSection>()
            .HasMany(x => x.Products)
            .WithOne(x => x.HomeSection)
            .HasForeignKey(x => x.HomeSectionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<HomeSectionProduct>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<TelegramOrderNotification>()
            .HasOne(x => x.Order)
            .WithMany(x => x.TelegramNotifications)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<TelegramOrderNotification>()
            .HasOne(x => x.AdminUser)
            .WithMany(x => x.TelegramOrderNotifications)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureQueryFilters(
        ModelBuilder builder)
    {
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

        builder.Entity<OrderStatusHistory>()
            .HasQueryFilter(x =>
                !x.IsDeleted &&
                !x.Order.IsDeleted);

        builder.Entity<PromoCode>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<PromoCodeUsage>()
            .HasQueryFilter(x =>
                !x.IsDeleted &&
                !x.PromoCode.IsDeleted);

        builder.Entity<WhatsAppProductInquiry>()
            .HasQueryFilter(x =>
                !x.IsDeleted &&
                !x.Product.IsDeleted);

        builder.Entity<StoreInfo>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<RefreshToken>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<AuditLog>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<UserAddress>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<PromoPage>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<PromoPageProduct>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<HomeSection>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<HomeSectionProduct>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<EmailAnnouncement>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<BasketLowStockEmailLog>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<CourierPhone>()
            .HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<TelegramOrderNotification>()
            .HasQueryFilter(x => !x.IsDeleted);
    }
}
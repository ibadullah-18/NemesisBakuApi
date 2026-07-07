using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string CustomerFullName { get; set; } = null!;
    public string CustomerPhoneNumber { get; set; } = null!;

    public DeliveryType DeliveryType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public string? AddressText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? BuildingNumber { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }

    public decimal? DeliveryDistanceKm { get; set; }

    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTimeRange { get; set; }

    public decimal DeliveryPrice { get; set; }
    public string? Note { get; set; }

    public decimal TotalProductPrice { get; set; }
    public decimal PromoDiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }

    public bool StockReturned { get; set; } = false;
    public DateTime? StockReturnedAt { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public bool IsWhatsappMessageSent { get; set; } = false;
    public DateTime? WhatsappMessageSentAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
}
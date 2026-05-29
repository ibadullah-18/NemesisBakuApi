using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;

    public string CustomerFullName { get; set; } = null!;
    public string CustomerPhoneNumber { get; set; } = null!;

    public DeliveryType DeliveryType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public string? AddressText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTimeRange { get; set; }

    public decimal DeliveryPrice { get; set; }
    public string? Note { get; set; }

    public decimal TotalProductPrice { get; set; }
    public decimal PromoDiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }

    public OrderStatus Status { get; set; }

    public bool IsWhatsappMessageSent { get; set; }
    public DateTime? WhatsappMessageSentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}
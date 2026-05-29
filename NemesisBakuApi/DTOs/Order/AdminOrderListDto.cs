using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class AdminOrderListDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;

    public string CustomerFullName { get; set; } = null!;
    public string CustomerPhoneNumber { get; set; } = null!;

    public decimal TotalPrice { get; set; }

    public DeliveryType DeliveryType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }

    public bool IsWhatsappMessageSent { get; set; }

    public DateTime CreatedAt { get; set; }
}
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class OrderListDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;

    public decimal TotalPrice { get; set; }

    public DeliveryType DeliveryType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
}
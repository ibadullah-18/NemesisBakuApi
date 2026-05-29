using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class UpdateOrderStatusDto
{
    public OrderStatus NewStatus { get; set; }
    public string? Note { get; set; }
}
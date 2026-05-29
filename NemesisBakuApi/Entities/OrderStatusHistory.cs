using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Entities;

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public OrderStatus OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Note { get; set; }
}
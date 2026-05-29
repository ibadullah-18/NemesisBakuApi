namespace NemesisBakuApi.Enums;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Preparing = 3,
    OnDelivery = 4,
    Delivered = 5,
    Cancelled = 6,
    Rejected = 7
}
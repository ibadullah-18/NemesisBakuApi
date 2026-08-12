using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Helpers;

public static class OrderStatusRules
{
    public static bool CanTransition(
        OrderStatus currentStatus,
        OrderStatus newStatus)
    {
        if (currentStatus == newStatus)
            return false;

        return currentStatus switch
        {
            OrderStatus.Pending =>
                newStatus is
                    OrderStatus.Confirmed or
                    OrderStatus.Cancelled or
                    OrderStatus.Rejected,

            OrderStatus.Confirmed =>
                newStatus is
                    OrderStatus.Preparing or
                    OrderStatus.OnDelivery or
                    OrderStatus.Cancelled or
                    OrderStatus.Rejected,

            OrderStatus.Preparing =>
                newStatus is
                    OrderStatus.OnDelivery or
                    OrderStatus.Cancelled or
                    OrderStatus.Rejected,

            OrderStatus.OnDelivery =>
                newStatus is
                    OrderStatus.Delivered or
                    OrderStatus.Cancelled,

            OrderStatus.Delivered => false,
            OrderStatus.Cancelled => false,
            OrderStatus.Rejected => false,

            _ => false
        };
    }

    public static bool RequiresStockReturn(
        OrderStatus status)
    {
        return status is
            OrderStatus.Cancelled or
            OrderStatus.Rejected;
    }

    public static string GetTransitionErrorMessage(
        OrderStatus currentStatus,
        OrderStatus newStatus)
    {
        return
            $"Sifariş statusunu {currentStatus} statusundan " +
            $"{newStatus} statusuna dəyişmək olmaz";
    }
}
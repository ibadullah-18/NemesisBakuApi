namespace NemesisBakuApi.DTOs.Stats;

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }

    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ConfirmedOrders { get; set; }
    public int OnDeliveryOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int CancelledOrders { get; set; }

    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockProducts { get; set; }

    public decimal TotalRevenue { get; set; }

    public int TotalPageViews { get; set; }
    public int UniqueVisitors { get; set; }

    public int WhatsAppProductClicks { get; set; }
    public int WhatsAppBasketClicks { get; set; }
    public int TotalWhatsAppClicks { get; set; }
}
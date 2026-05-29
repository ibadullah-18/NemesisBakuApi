namespace NemesisBakuApi.DTOs.Stats;

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }

    public decimal TotalRevenue { get; set; }

    public int PendingOrders { get; set; }
    public int DeliveredOrders { get; set; }

    public int TotalPageViews { get; set; }
    public int UniqueVisitors { get; set; }

    public int WhatsAppProductClicks { get; set; }
    public int WhatsAppBasketClicks { get; set; }
    public int TotalWhatsAppClicks { get; set; }
}
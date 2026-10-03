namespace NemesisBakuApi.DTOs.Order;

public class CalculateDeliveryResultDto
{
    public decimal? DistanceKm { get; set; }
    public decimal DeliveryPrice { get; set; }
    public Guid? MetroStationId { get; set; }
    public string? MetroStationName { get; set; }
    public decimal? MetroDistanceKm { get; set; }
    public string PricingRule { get; set; } = "store-distance";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

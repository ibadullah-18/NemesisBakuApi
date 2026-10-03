namespace NemesisBakuApi.DTOs.Order;

public class CalculateDeliveryDto
{
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public NemesisBakuApi.Enums.DeliveryType DeliveryType { get; set; } = NemesisBakuApi.Enums.DeliveryType.HomeDelivery;
    public Guid? MetroStationId { get; set; }
}

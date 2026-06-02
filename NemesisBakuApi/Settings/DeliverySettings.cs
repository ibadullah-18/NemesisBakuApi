namespace NemesisBakuApi.Settings;

public class DeliverySettings
{
    public decimal StoreLatitude { get; set; }
    public decimal StoreLongitude { get; set; }

    public decimal MinimumPrice { get; set; } = 5;
    public decimal PricePerKm { get; set; } = 1;
}
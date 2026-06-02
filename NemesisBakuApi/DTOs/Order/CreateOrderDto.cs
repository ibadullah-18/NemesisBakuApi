using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class CreateOrderDto
{
    public List<OrderItemCreateDto> Items { get; set; } = new();

    public string CustomerFullName { get; set; } = null!;
    public string CustomerPhoneNumber { get; set; } = null!;

    public DeliveryType DeliveryType { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public string? AddressText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? BuildingNumber { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }  

    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTimeRange { get; set; }

    public string? Note { get; set; }

    public string? PromoCode { get; set; }
}

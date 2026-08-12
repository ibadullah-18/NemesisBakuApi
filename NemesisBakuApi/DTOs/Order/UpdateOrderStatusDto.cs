using System.ComponentModel.DataAnnotations;
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Order;

public class UpdateOrderStatusDto
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus NewStatus { get; set; }

    [StringLength(
        500,
        ErrorMessage = "Qeyd maksimum 500 simvol ola bilər")]
    public string? Note { get; set; }
}
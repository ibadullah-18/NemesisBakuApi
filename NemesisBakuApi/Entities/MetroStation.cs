using System.ComponentModel.DataAnnotations;

namespace NemesisBakuApi.Entities;

public class MetroStation : BaseEntity
{
    [MaxLength(120)] public string Name { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
}

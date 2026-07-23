namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageUpdateDto
{
    public IFormFile? File { get; set; }
    public IFormFile? MobileFile { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> ProductIds { get; set; } = new();
}

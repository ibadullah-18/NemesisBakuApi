namespace NemesisBakuApi.Entities;

public class WhatsAppMessageLog : BaseEntity
{
    public string ToPhoneNumber { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public string MessageBody { get; set; } = null!;

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid? UserId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ProductId { get; set; }
}
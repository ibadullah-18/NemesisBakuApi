namespace NemesisBakuApi.Settings;

public class WhatsAppSettings
{
    public string PhoneNumberId { get; set; } = null!;
    public string BusinessAccountId { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public string SellerPhoneNumber { get; set; } = null!;
    public string ApiVersion { get; set; } = "v25.0";
}
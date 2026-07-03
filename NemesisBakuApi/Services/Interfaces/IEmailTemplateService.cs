namespace NemesisBakuApi.Services.Interfaces;

public interface IEmailTemplateService
{
    string Build(
        string title,
        string description,
        string? buttonText = null,
        string? buttonUrl = null);
}
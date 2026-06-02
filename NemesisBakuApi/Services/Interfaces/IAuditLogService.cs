namespace NemesisBakuApi.Services.Interfaces;

public interface IAuditLogService
{
    Task CreateAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId,
        string? description,
        string? ipAddress,
        string? userAgent);
}
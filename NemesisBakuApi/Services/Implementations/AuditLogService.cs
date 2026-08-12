using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private const int MaxDescriptionLength = 4000;
    private const int MaxUserAgentLength = 1000;
    private const int MaxIpAddressLength = 100;

    private readonly AppDbContext _context;

    public AuditLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId,
        string? description,
        string? ipAddress,
        string? userAgent)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,

            Description = Limit(
                description,
                MaxDescriptionLength),

            IpAddress = Limit(
                ipAddress,
                MaxIpAddressLength),

            UserAgent = Limit(
                userAgent,
                MaxUserAgentLength)
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }

    private static string? Limit(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
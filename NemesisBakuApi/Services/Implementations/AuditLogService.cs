using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class AuditLogService : IAuditLogService
{
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
            Description = description,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public sealed class DatabaseCleanupService
    : IDatabaseCleanupService
{
    private const int TelegramMaxAttempts = 8;

    private readonly AppDbContext _context;
    private readonly DatabaseCleanupSettings _settings;

    public DatabaseCleanupService(
        AppDbContext context,
        IOptions<DatabaseCleanupSettings> options)
    {
        _context = context;
        _settings = options.Value;
    }

    public async Task<DatabaseCleanupResult> CleanupAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var deletedByTable =
            new Dictionary<string, int>(
                StringComparer.Ordinal);

        deletedByTable[nameof(AppDbContext.SiteVisits)] =
            await DeleteInBatchesAsync(
                _context.SiteVisits
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.VisitedAt < now.AddDays(
                            -RetentionDays(
                                _settings.SiteVisitRetentionDays))),
                x => x.VisitedAt,
                cancellationToken);

        var whatsAppCutoff = now.AddDays(
            -RetentionDays(
                _settings.WhatsAppLogRetentionDays));

        deletedByTable[nameof(AppDbContext.WhatsAppClickLogs)] =
            await DeleteInBatchesAsync(
                _context.WhatsAppClickLogs
                    .IgnoreQueryFilters()
                    .Where(x => x.CreatedAt < whatsAppCutoff),
                x => x.CreatedAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.WhatsAppProductInquiries)] =
            await DeleteInBatchesAsync(
                _context.WhatsAppProductInquiries
                    .IgnoreQueryFilters()
                    .Where(x => x.CreatedAt < whatsAppCutoff),
                x => x.CreatedAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.WhatsAppMessageLogs)] =
            await DeleteInBatchesAsync(
                _context.WhatsAppMessageLogs
                    .IgnoreQueryFilters()
                    .Where(x => x.CreatedAt < whatsAppCutoff),
                x => x.CreatedAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.UserActivityLogs)] =
            await DeleteInBatchesAsync(
                _context.UserActivityLogs
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.CreatedAt < now.AddDays(
                            -RetentionDays(
                                _settings.UserActivityRetentionDays))),
                x => x.CreatedAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.AuditLogs)] =
            await DeleteInBatchesAsync(
                _context.AuditLogs
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.CreatedAt < now.AddDays(
                            -RetentionDays(
                                _settings.AuditLogRetentionDays))),
                x => x.CreatedAt,
                cancellationToken);

        var telegramCutoff = now.AddDays(
            -RetentionDays(
                _settings.TelegramNotificationRetentionDays));

        deletedByTable[nameof(AppDbContext.TelegramOrderNotifications)] =
            await DeleteInBatchesAsync(
                _context.TelegramOrderNotifications
                    .IgnoreQueryFilters()
                    .Where(x =>
                        (x.SentAt.HasValue &&
                         x.SentAt.Value < telegramCutoff) ||
                        (x.AttemptCount >= TelegramMaxAttempts &&
                         (x.UpdatedAt ?? x.CreatedAt) < telegramCutoff)),
                x => x.CreatedAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.BasketLowStockEmailLogs)] =
            await DeleteInBatchesAsync(
                _context.BasketLowStockEmailLogs
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.SentAt < now.AddDays(
                            -RetentionDays(
                                _settings.BasketLowStockLogRetentionDays))),
                x => x.SentAt,
                cancellationToken);

        deletedByTable[nameof(AppDbContext.EmailAnnouncements)] =
            await DeleteInBatchesAsync(
                _context.EmailAnnouncements
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.CreatedAt < now.AddDays(
                            -RetentionDays(
                                _settings.EmailAnnouncementRetentionDays)) &&
                        x.SentCount + x.FailedCount >=
                        x.TotalRecipients),
                x => x.CreatedAt,
                cancellationToken);

        return new DatabaseCleanupResult(deletedByTable);
    }

    private async Task<int> DeleteInBatchesAsync<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, DateTime>> orderBy,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var batchSize = Math.Clamp(
            _settings.BatchSize,
            50,
            2000);

        var maxBatches = Math.Clamp(
            _settings.MaxBatchesPerTable,
            1,
            50);

        var totalDeleted = 0;

        for (var batch = 0; batch < maxBatches; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = await source
                .AsNoTracking()
                .OrderBy(orderBy)
                .Select(x => x.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                break;
            }

            var deleted = await _context.Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);

            totalDeleted += deleted;

            if (ids.Count < batchSize)
            {
                break;
            }
        }

        return totalDeleted;
    }

    private static int RetentionDays(int value) =>
        Math.Clamp(value, 7, 3650);
}
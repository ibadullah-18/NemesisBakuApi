using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public sealed class EmailAnnouncementWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailAnnouncementWorkerSettings _settings;
    private readonly ILogger<EmailAnnouncementWorker> _logger;

    public EmailAnnouncementWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailAnnouncementWorkerSettings> options,
        ILogger<EmailAnnouncementWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var initialDelay = TimeSpan.FromSeconds(
            Math.Max(1, _settings.InitialDelaySeconds));

        var pollInterval = TimeSpan.FromSeconds(
            Math.Max(2, _settings.PollIntervalSeconds));

        try
        {
            await Task.Delay(initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var processed = await ProcessNextBatchSafelyAsync(
                    stoppingToken);

                if (!processed)
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> ProcessNextBatchSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessNextBatchAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Email elan batch-i göndərilərkən xəta baş verdi");

            return false;
        }
    }

    private async Task<bool> ProcessNextBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var emailService = scope.ServiceProvider
            .GetRequiredService<IEmailService>();

        var announcement = await context.EmailAnnouncements
            .Where(x =>
                x.TotalRecipients >
                x.SentCount + x.FailedCount)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (announcement == null)
        {
            return false;
        }

        var processedCount =
            announcement.SentCount +
            announcement.FailedCount;

        var remainingCount =
            announcement.TotalRecipients -
            processedCount;

        var batchSize = Math.Min(
            Math.Clamp(_settings.BatchSize, 1, 50),
            remainingCount);

        var emails = await context.Users
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.Email != null &&
                x.Email != "")
            .OrderBy(x => x.Id)
            .Skip(processedCount)
            .Take(batchSize)
            .Select(x => x.Email!)
            .ToListAsync(cancellationToken);

        if (emails.Count == 0)
        {
            announcement.TotalRecipients = processedCount;
            announcement.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return true;
        }

        foreach (var email in emails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sent = await emailService.SendAnnouncementAsync(
                email,
                announcement.Title,
                announcement.Description,
                announcement.ButtonText,
                announcement.ButtonUrl);

            if (sent)
            {
                announcement.SentCount++;
            }
            else
            {
                announcement.FailedCount++;
            }
        }

        announcement.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Email elan batch-i tamamlandı. " +
            "AnnouncementId: {AnnouncementId}, " +
            "Processed: {Processed}, Total: {Total}",
            announcement.Id,
            announcement.SentCount + announcement.FailedCount,
            announcement.TotalRecipients);

        return true;
    }
}
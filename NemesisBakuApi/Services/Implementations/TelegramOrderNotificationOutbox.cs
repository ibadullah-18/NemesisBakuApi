using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramOrderNotificationOutbox
    : ITelegramOrderNotificationOutbox
{
    private readonly AppDbContext _context;

    public TelegramOrderNotificationOutbox(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var recipients = await (
            from user in _context.Users.AsNoTracking()

            join userRole in
                _context.UserRoles.AsNoTracking()
                on user.Id equals userRole.UserId

            join role in
                _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id

            where
                !user.IsDeleted &&
                user.IsActive &&
                user.TelegramNotificationsEnabled &&
                user.TelegramChatId.HasValue &&
                (role.Name == "Admin" ||
                 role.Name == "SuperAdmin")

            select new
            {
                UserId = user.Id,
                user.TelegramChatId,
                user.FullName,
                RoleName = role.Name!
            })
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var notifications = recipients
            .GroupBy(x => x.UserId)
            .Select(group =>
            {
                var recipient = group.First();

                var panelRole = group.Any(
                    x => x.RoleName == "SuperAdmin")
                        ? "SuperAdmin"
                        : "Admin";

                return new TelegramOrderNotification
                {
                    OrderId = order.Id,
                    AdminUserId = recipient.UserId,

                    TelegramChatId =
                        recipient.TelegramChatId!.Value,

                    AdminFullName =
                        recipient.FullName,

                    PanelRole = panelRole,
                    AttemptCount = 0,
                    NextAttemptAt = now
                };
            })
            .ToList();

        _context.TelegramOrderNotifications
            .AddRange(notifications);
    }
}
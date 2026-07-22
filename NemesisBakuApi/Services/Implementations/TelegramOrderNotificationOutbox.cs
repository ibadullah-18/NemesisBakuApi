using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramOrderNotificationOutbox
    : ITelegramOrderNotificationOutbox
{
    private readonly AppDbContext _context;

    public TelegramOrderNotificationOutbox(AppDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var recipients = await (
            from user in _context.Users
            join userRole in _context.UserRoles on user.Id equals userRole.UserId
            join role in _context.Roles on userRole.RoleId equals role.Id
            where
                !user.IsDeleted &&
                user.IsActive &&
                user.TelegramNotificationsEnabled &&
                user.TelegramChatId.HasValue &&
                (role.Name == "Admin" || role.Name == "SuperAdmin")
            select new
            {
                User = user,
                RoleName = role.Name!
            })
            .ToListAsync(cancellationToken);

        foreach (var recipientGroup in recipients.GroupBy(x => x.User.Id))
        {
            var recipient = recipientGroup.First().User;

            var panelRole = recipientGroup.Any(x => x.RoleName == "SuperAdmin")
                ? "SuperAdmin"
                : "Admin";

            _context.TelegramOrderNotifications.Add(
                new TelegramOrderNotification
                {
                    OrderId = order.Id,
                    AdminUserId = recipient.Id,
                    TelegramChatId = recipient.TelegramChatId!.Value,
                    AdminFullName = recipient.FullName,
                    PanelRole = panelRole,
                    AttemptCount = 0,
                    NextAttemptAt = DateTime.UtcNow
                });
        }
    }
}

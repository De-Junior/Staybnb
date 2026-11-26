using Microsoft.AspNetCore.Identity;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string? body, NotificationType type, string? linkUrl = null);
    Task NotifyAdminsAsync(string title, string? body, NotificationType type, string? linkUrl = null);
}

public class NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : INotificationService
{
    public async Task NotifyAsync(string userId, string title, string? body, NotificationType type, string? linkUrl = null)
    {
        db.Notifications.Add(Create(userId, title, body, type, linkUrl));
        await db.SaveChangesAsync();
    }

    public async Task NotifyAdminsAsync(string title, string? body, NotificationType type, string? linkUrl = null)
    {
        var admins = (await userManager.GetUsersInRoleAsync(Roles.Admin))
            .Concat(await userManager.GetUsersInRoleAsync(Roles.SuperAdmin))
            .DistinctBy(u => u.Id);

        foreach (var admin in admins)
            db.Notifications.Add(Create(admin.Id, title, body, type, linkUrl));

        await db.SaveChangesAsync();
    }

    private static Notification Create(string userId, string title, string? body, NotificationType type, string? linkUrl) => new()
    {
        UserId = userId,
        Title = title.Length > 100 ? title[..100] : title,
        Body = body is { Length: > 500 } ? body[..500] : body,
        Type = type,
        LinkUrl = linkUrl
    };
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Services;

public record UnreadCounts(int Notifications, int Messages);

public interface IUnreadCounter
{
    Task<UnreadCounts> GetAsync(ClaimsPrincipal user);
}

public class UnreadCounter(ApplicationDbContext db) : IUnreadCounter
{
    public async Task<UnreadCounts> GetAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return new UnreadCounts(0, 0);

        var notifications = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        var messages = await db.Messages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);
        return new UnreadCounts(notifications, messages);
    }
}

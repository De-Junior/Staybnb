using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController(ApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(string filter = "all", int? selectedId = null)
    {
        var mine = db.Notifications.Where(n => n.UserId == CurrentUserId);

        // Viewing a notification marks it as read
        if (selectedId.HasValue)
        {
            await mine.Where(n => n.Id == selectedId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        var todayUtc = DateTime.UtcNow.Date;
        var filtered = filter switch
        {
            "unread" => mine.Where(n => !n.IsRead),
            "read" => mine.Where(n => n.IsRead),
            "today" => mine.Where(n => n.CreatedAt >= todayUtc),
            _ => mine
        };

        var list = await filtered.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();

        return View(new NotificationsViewModel
        {
            Notifications = list,
            Filter = filter,
            Selected = selectedId.HasValue ? await mine.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedId) : null,
            AllCount = await mine.CountAsync(),
            UnreadCount = await mine.CountAsync(n => !n.IsRead),
            ReadCount = await mine.CountAsync(n => n.IsRead),
            TodayCount = await mine.CountAsync(n => n.CreatedAt >= todayUtc)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(int id)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (notification is null) return NotFound();

        notification.IsRead = true;
        await db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
            return LocalRedirect(notification.LinkUrl);

        return RedirectToAction(nameof(Index), new { selectedId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string filter = "all")
    {
        await db.Notifications.Where(n => n.Id == id && n.UserId == CurrentUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string filter = "all")
    {
        await db.Notifications.Where(n => n.UserId == CurrentUserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        Success("All notifications marked as read.");
        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string filter = "all")
    {
        await db.Notifications.Where(n => n.Id == id && n.UserId == CurrentUserId).ExecuteDeleteAsync();
        return RedirectToAction(nameof(Index), new { filter });
    }
}

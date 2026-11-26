using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize]
public class MessagesController(ApplicationDbContext db, INotificationService notifications) : AppController
{
    private const int MaxLength = 2000;

    public async Task<IActionResult> Index(string? with)
    {
        var me = CurrentUserId;
        ApplicationUser? selected = null;

        if (!string.IsNullOrEmpty(with) && with != me)
        {
            selected = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == with);
            if (selected is not null)
            {
                // Opening a conversation marks its incoming messages as read
                await db.Messages
                    .Where(m => m.SenderId == with && m.ReceiverId == me && !m.IsRead)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
            }
        }

        var messages = await db.Messages.AsNoTracking()
            .Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == me || m.ReceiverId == me)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var conversations = messages
            .GroupBy(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
            .Select(g => new ConversationViewModel
            {
                LastMessage = g.First(),
                OtherUser = g.First().SenderId == me ? g.First().Receiver : g.First().Sender,
                UnreadCount = g.Count(m => m.ReceiverId == me && !m.IsRead)
            })
            .ToList();

        return View(new MessagesViewModel
        {
            CurrentUserId = me,
            Conversations = conversations,
            SelectedUser = selected,
            Thread = selected is null
                ? []
                : messages.Where(m => m.SenderId == selected.Id || m.ReceiverId == selected.Id)
                          .OrderBy(m => m.Timestamp).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string? content)
    {
        var me = CurrentUserId;
        content = content?.Trim();

        if (string.IsNullOrEmpty(content))
        {
            Error("Message cannot be empty.");
            return RedirectToAction(nameof(Index), new { with = receiverId });
        }

        if (content.Length > MaxLength)
        {
            Error($"Messages are limited to {MaxLength} characters.");
            return RedirectToAction(nameof(Index), new { with = receiverId });
        }

        var sender = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me);
        if (receiverId == me || !await db.Users.AnyAsync(u => u.Id == receiverId))
            return BadRequest();

        db.Messages.Add(new Message { SenderId = me, ReceiverId = receiverId, Content = content });
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(receiverId, "New Message",
            $"{sender.FullName}: {(content.Length > 120 ? content[..120] + "..." : content)}",
            NotificationType.Message, $"/Messages?with={me}");

        return RedirectToAction(nameof(Index), new { with = receiverId });
    }
}

using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IActivityLogger
{
    Task LogAsync(string? userId, string action, ActivityType type, string? details = null);
}

public class ActivityLogger(ApplicationDbContext db) : IActivityLogger
{
    public async Task LogAsync(string? userId, string action, ActivityType type, string? details = null)
    {
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = Truncate(action, 100)!,
            ActivityType = type,
            Details = Truncate(details, 500)
        });
        await db.SaveChangesAsync();
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}

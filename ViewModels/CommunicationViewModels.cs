using Staybnb.Models;

namespace Staybnb.ViewModels;

public class ConversationViewModel
{
    public ApplicationUser OtherUser { get; set; } = null!;
    public Message LastMessage { get; set; } = null!;
    public int UnreadCount { get; set; }
}

public class MessagesViewModel
{
    public List<ConversationViewModel> Conversations { get; set; } = [];
    public ApplicationUser? SelectedUser { get; set; }
    public List<Message> Thread { get; set; } = [];
    public string CurrentUserId { get; set; } = string.Empty;
}

public class NotificationsViewModel
{
    public List<Notification> Notifications { get; set; } = [];
    public Notification? Selected { get; set; }
    public string Filter { get; set; } = "all";
    public int AllCount { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public int TodayCount { get; set; }
}

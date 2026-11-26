using Staybnb.Models;

namespace Staybnb.ViewModels;

public class HostDashboardViewModel
{
    public string HostName { get; set; } = string.Empty;
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int BookingsThisMonth { get; set; }
    public int PendingRequests { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal EarningsThisMonth { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int CheckInsToVerify { get; set; }
    public List<Booking> RecentRequests { get; set; } = [];
    public List<Booking> UpcomingStays { get; set; } = [];
}

public class HostPropertyRowViewModel
{
    public HostProperty Property { get; set; } = null!;
    public ApplicationStatus? LatestApplicationStatus { get; set; }
    public int BookingCount { get; set; }
    public int ReviewCount { get; set; }
    public double AverageRating { get; set; }
    public bool IsApproved => LatestApplicationStatus == ApplicationStatus.Approved;
}

public class EarningsViewModel
{
    public decimal Total { get; set; }
    public List<(string Month, decimal Amount)> ByMonth { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveProperties { get; set; }
    public decimal PlatformRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }
    public List<ActivityLog> RecentActivity { get; set; } = [];
    public List<HostApplication> RecentApplications { get; set; } = [];
    public List<Booking> RecentBookings { get; set; } = [];
}

public class UserRowViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = [];
}

public class AdminUsersViewModel
{
    public List<UserRowViewModel> Users { get; set; } = [];
    public string? Search { get; set; }
    public string? Role { get; set; }
}

public class ActivityLogsViewModel
{
    public List<ActivityLog> Logs { get; set; } = [];
    public ActivityType? Type { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

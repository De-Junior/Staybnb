using System.Globalization;
using System.Security.Claims;
using Staybnb.Models;

namespace Staybnb.Helpers;

public static class Fmt
{
    private static readonly NumberFormatInfo Rand = new()
    {
        CurrencySymbol = "R",
        CurrencyGroupSeparator = " ",
        CurrencyDecimalSeparator = ".",
        CurrencyPositivePattern = 2 // "R 1 234"
    };

    public static string Money(decimal amount, int decimals = 0) => amount.ToString("C" + decimals, Rand);

    public static string Date(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Stamp(DateTime utc) =>
        utc.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return Date(utc.ToLocalTime());
    }

    public static string Initial(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();

    public static string Initials(string first, string last) =>
        $"{(first.Length > 0 ? first[0] : ' ')}{(last.Length > 0 ? last[0] : ' ')}".Trim().ToUpperInvariant();

    public static string Badge(BookingStatus s) => s switch
    {
        BookingStatus.Pending => "badge-soft-warning",
        BookingStatus.Approved => "badge-soft-success",
        BookingStatus.CheckedIn => "badge-soft-info",
        BookingStatus.Completed => "badge-soft-secondary",
        _ => "badge-soft-danger"
    };

    public static string Badge(ApplicationStatus s) => s switch
    {
        ApplicationStatus.Pending => "badge-soft-warning",
        ApplicationStatus.Approved => "badge-soft-success",
        _ => "badge-soft-danger"
    };

    public static string Badge(PaymentStatus s) => s switch
    {
        PaymentStatus.Paid => "badge-soft-info",
        PaymentStatus.Pending => "badge-soft-warning",
        PaymentStatus.Refunded => "badge-soft-secondary",
        _ => "badge-soft-danger"
    };

    public static string Badge(CheckInStatus s) => s switch
    {
        CheckInStatus.Verified => "badge-soft-success",
        CheckInStatus.Submitted => "badge-soft-info",
        CheckInStatus.InProgress => "badge-soft-warning",
        CheckInStatus.Rejected => "badge-soft-danger",
        _ => "badge-soft-secondary"
    };

    public static string Badge(DocumentStatus s) => s switch
    {
        DocumentStatus.Verified => "badge-soft-success",
        DocumentStatus.Rejected => "badge-soft-danger",
        _ => "badge-soft-warning"
    };

    public static string Icon(NotificationType t) => t switch
    {
        NotificationType.Success => "bi-check-circle-fill text-success",
        NotificationType.Warning => "bi-exclamation-triangle-fill text-warning",
        NotificationType.Booking => "bi-calendar-check text-primary",
        NotificationType.Message => "bi-envelope-fill text-primary",
        NotificationType.HostApplication => "bi-house-add-fill text-primary",
        NotificationType.CheckIn => "bi-person-vcard text-info",
        _ => "bi-info-circle-fill text-secondary"
    };

    public static string FullName(ClaimsPrincipal user) =>
        user.FindFirstValue(Services.AppClaimsPrincipalFactory.FullNameClaim) ?? user.Identity?.Name ?? "";

    public static string RoleLabel(ClaimsPrincipal user) =>
        user.IsInRole(Roles.SuperAdmin) ? "Super Admin"
        : user.IsInRole(Roles.Admin) ? "Administrator"
        : user.IsInRole(Roles.Host) ? "Host"
        : "Guest";

    public static string Layout(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Host) || user.IsInRole(Roles.Admin) || user.IsInRole(Roles.SuperAdmin)
            ? "_DashboardLayout"
            : "_Layout";
}

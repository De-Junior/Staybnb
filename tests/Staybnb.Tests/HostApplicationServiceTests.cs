using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Tests;

public class HostApplicationServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly ServiceProvider _services;
    private readonly UserManager<ApplicationUser> _users;
    private readonly HostApplicationService _service;

    public HostApplicationServiceTests()
    {
        // The real Identity stores on the same test database, so role changes are the app's own.
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_t.Db)
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .Services.BuildServiceProvider();

        _users = _services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = _services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Guest, Roles.Host, Roles.Admin })
            roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();

        _service = new HostApplicationService(_t.Db, _users, new NoopFileStorage(), new NoopActivityLogger(), new NoopNotifications());
    }

    public void Dispose()
    {
        _services.Dispose();
        _t.Dispose();
    }

    private async Task<(ApplicationUser Applicant, HostApplication Application)> GuestWithPendingApplication()
    {
        var applicant = _t.AddUser("applicant@test.local");
        await _users.AddToRoleAsync(applicant, Roles.Guest);
        var property = _t.AddProperty(applicant, active: false);
        var application = new HostApplication { ApplicationUserId = applicant.Id, PropertyId = property.Id };
        _t.Db.HostApplications.Add(application);
        await _t.Db.SaveChangesAsync();
        return (applicant, application);
    }

    [Fact]
    public async Task Approving_swaps_the_Guest_role_for_Host_and_makes_the_property_live()
    {
        var (applicant, application) = await GuestWithPendingApplication();

        var result = await _service.ApproveAsync(application.Id, "admin-id", "Looks good");

        Assert.True(result.Succeeded, result.Message);
        Assert.True(await _users.IsInRoleAsync(applicant, Roles.Host));
        Assert.False(await _users.IsInRoleAsync(applicant, Roles.Guest));

        var saved = await _t.Db.HostApplications.Include(a => a.Property).ThenInclude(p => p.CheckInProcess)
            .AsNoTracking().SingleAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Approved, saved.Status);
        Assert.True(saved.Property.IsActive);
        Assert.NotNull(saved.Property.CheckInProcess);
        Assert.NotNull(saved.ReviewedAt);
    }

    [Fact]
    public async Task An_application_can_only_be_reviewed_once()
    {
        var (_, application) = await GuestWithPendingApplication();

        Assert.True((await _service.ApproveAsync(application.Id, "admin-id", null)).Succeeded);
        var second = await _service.ApproveAsync(application.Id, "admin-id", null);

        Assert.False(second.Succeeded);
        Assert.Contains("already been reviewed", second.Message);
    }

    [Fact]
    public async Task An_unknown_application_fails_cleanly()
    {
        var result = await _service.ApproveAsync(9999, "admin-id", null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Rejecting_leaves_the_applicant_a_Guest_and_the_property_inactive()
    {
        var (applicant, application) = await GuestWithPendingApplication();

        var result = await _service.RejectAsync(application.Id, "admin-id", "Photos are unclear");

        Assert.True(result.Succeeded, result.Message);
        Assert.True(await _users.IsInRoleAsync(applicant, Roles.Guest));
        Assert.False(await _users.IsInRoleAsync(applicant, Roles.Host));

        var saved = await _t.Db.HostApplications.Include(a => a.Property).AsNoTracking().SingleAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Rejected, saved.Status);
        Assert.False(saved.Property.IsActive);
    }
}

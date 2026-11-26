using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IActivityLogger activity,
    INotificationService notifications) : AppController
{
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim()
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Every new registrant starts as a Guest
        await userManager.AddToRoleAsync(user, Roles.Guest);
        await activity.LogAsync(user.Id, "UserRegistered", ActivityType.Authentication, $"{user.Email} registered as Guest");
        await notifications.NotifyAsync(user.Id, "Welcome to Staybnb",
            "Your account is ready. Explore stays, or apply to become a Host.", NotificationType.Info, "/");

        await signInManager.SignInAsync(user, isPersistent: false);
        await activity.LogAsync(user.Id, "LoginSucceeded", ActivityType.Authentication, user.Email);

        Success($"Welcome, {user.FirstName}!");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            await activity.LogAsync(user?.Id, result.IsLockedOut ? "LoginLockedOut" : "LoginFailed",
                ActivityType.Authentication, model.Email);

            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "This account is locked after too many failed attempts. Try again in a few minutes."
                : "Invalid email or password.");
            return View(model);
        }

        await activity.LogAsync(user!.Id, "LoginSucceeded", ActivityType.Authentication, user.Email);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.Admin)) return RedirectToAction("Dashboard", "Admin");
        if (roles.Contains(Roles.Host)) return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await activity.LogAsync(CurrentUserId, "Logout", ActivityType.Authentication, User.Identity?.Name);
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}

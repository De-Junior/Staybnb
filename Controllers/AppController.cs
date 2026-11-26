using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

public abstract class AppController : Controller
{
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    protected void Success(string message) => TempData["Success"] = message;

    protected void Error(string message) => TempData["Error"] = message;

    protected IActionResult RedirectToRoleHome()
    {
        if (User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.Admin))
            return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole(Roles.Host))
            return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Home");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize]
public class DocumentsController(ApplicationDbContext db, IFileStorage storage) : AppController
{
    // Private ID documents are served only to the uploading guest, the property's host, or an admin.
    public async Task<IActionResult> Open(int id)
    {
        var doc = await db.GuestDocuments.AsNoTracking()
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc is null) return NotFound();

        var booking = doc.GuestCheckIn.Booking;
        var allowed = booking.GuestId == CurrentUserId
                      || booking.Property.HostId == CurrentUserId
                      || User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin);
        if (!allowed) return Forbid();

        var path = storage.GetGuestDocumentPath(doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        return PhysicalFile(path, storage.GetContentType(doc.FileName));
    }
}

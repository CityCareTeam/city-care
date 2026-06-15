using CityCare.Api.Dtos.Notifications;
using CityCare.Api.Services;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("users/me/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly CityCareDbContext _db;
    private readonly CurrentUserService _currentUser;

    public NotificationsController(CityCareDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /users/me/notifications — Liste paginée + unread_count (badge)
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20,
        [FromQuery] bool unread_only = false,
        CancellationToken cancellationToken = default)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        if (page < 1) page = 1;
        if (page_size < 1 || page_size > 100) page_size = 20;

        var baseQuery = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == user.Id);

        var unreadCount = await baseQuery.CountAsync(n => !n.IsRead, cancellationToken);

        var query = unread_only ? baseQuery.Where(n => !n.IsRead) : baseQuery;
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * page_size)
            .Take(page_size)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data         = items.Select(NotificationResponse.From).ToList(),
            unread_count = unreadCount,
            pagination   = new
            {
                page,
                page_size,
                total_count = totalCount,
                total_pages = (int)Math.Ceiling((double)totalCount / page_size)
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /users/me/notifications/unread-count — Badge de la cloche
    // ─────────────────────────────────────────────────────────────
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var count = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == user.Id && !n.IsRead, cancellationToken);

        return Ok(new { unread_count = count });
    }

    // ─────────────────────────────────────────────────────────────
    // PATCH /users/me/notifications/{id}/read — Marquer comme lu
    // ─────────────────────────────────────────────────────────────
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id, cancellationToken);

        if (notification is null)
            return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────
    // POST /users/me/notifications/read-all — Tout marquer comme lu
    // ─────────────────────────────────────────────────────────────
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        await _db.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────
    // DELETE /users/me/notifications/{id} — Supprimer une notification
    // ─────────────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOne(Guid id, CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id, cancellationToken);

        if (notification is null)
            return NotFound();

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────
    // DELETE /users/me/notifications — Supprimer toutes les notifications
    // ─────────────────────────────────────────────────────────────
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        await _db.Notifications
            .Where(n => n.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return NoContent();
    }
}

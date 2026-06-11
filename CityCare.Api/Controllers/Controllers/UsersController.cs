using System.Security.Claims;
using CityCare.Api.Services;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly CityCareDbContext _db;

    public UsersController(CityCareDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /users/me/incidents — Mes signalements (tâche 8)
    // ─────────────────────────────────────────────────────────────
    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized(new { error = "Missing or invalid user id claim." });

        var incidents = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.AuthorUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                id            = i.Id,
                type          = i.Type.ToString(),
                status        = IncidentService.ToSnakeCase(i.Status),
                address_label = i.AddressLabel,
                created_at    = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = incidents });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /users/me — Profil utilisateur (tâche 9)
    // ─────────────────────────────────────────────────────────────
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized(new { error = "Missing or invalid user id claim." });

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                id           = u.Id,
                email        = u.Email,
                display_name = u.DisplayName,
                role         = u.Role.ToString().ToLower(),
                created_at   = u.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return NotFound(new { error = "Utilisateur non trouvé." });

        return Ok(user);
    }
}

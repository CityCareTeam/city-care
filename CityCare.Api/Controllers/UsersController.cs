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

    // Récupère le KeycloakId depuis le claim "sub" du token JWT
    private string? GetKeycloakId() =>
        User.FindFirstValue("sub") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ─────────────────────────────────────────────────────────────
    // GET /users/me/incidents — Mes signalements
    // ─────────────────────────────────────────────────────────────
    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var keycloakId = GetKeycloakId();
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(new { error = "Token invalide : claim 'sub' manquant." });

        // Résoudre le Guid local via KeycloakId
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, cancellationToken);

        if (user is null)
            return NotFound(new { error = "Utilisateur non trouvé en base." });

        var incidents = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.AuthorUserId == user.Id)
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
    // GET /users/me — Profil utilisateur
    // ─────────────────────────────────────────────────────────────
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var keycloakId = GetKeycloakId();
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(new { error = "Token invalide : claim 'sub' manquant." });

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.KeycloakId == keycloakId)
            .Select(u => new
            {
                id           = u.Id,
                keycloak_id  = u.KeycloakId,
                email        = u.Email,
                display_name = u.DisplayName,
                role         = u.Role.ToString().ToLower(),
                created_at   = u.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return NotFound(new { error = "Utilisateur non trouvé en base." });

        return Ok(user);
    }
}

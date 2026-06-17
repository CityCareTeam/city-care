using CityCare.Api.Dtos.Admin;
using CityCare.Api.Services;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly KeycloakService _keycloak;
    private readonly CityCareDbContext _db;

    public AdminController(KeycloakService keycloak, CityCareDbContext db)
    {
        _keycloak = keycloak;
        _db = db;
    }

    // GET /admin/stats
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var totalIncidents = await _db.Incidents.CountAsync(cancellationToken);

        var todayUtc = DateTime.UtcNow.Date;
        var resolvedToday = await _db.Incidents
            .Where(i => i.Status == IncidentStatus.Resolved
                        && i.ResolvedAt.HasValue
                        && i.ResolvedAt.Value >= todayUtc
                        && i.ResolvedAt.Value < todayUtc.AddDays(1))
            .CountAsync(cancellationToken);

        var users = await _keycloak.GetUsersAsync(0, 200);
        var activeUsers = users.Count(u => u.Enabled);

        return Ok(new
        {
            total_incidents = totalIncidents,
            resolved_today  = resolvedToday,
            active_users    = activeUsers
        });
    }

    // GET /admin/users
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int first = 0,
        [FromQuery] int max   = 100,
        CancellationToken cancellationToken = default)
    {
        if (max  < 1 || max  > 200) max  = 100;
        if (first < 0)               first = 0;

        var users = await _keycloak.GetUsersAsync(first, max, search);

        var data = users.Select(u => new
        {
            id           = u.Id,
            username     = u.Username,
            email        = u.Email,
            first_name   = u.FirstName,
            last_name    = u.LastName,
            display_name = string.Join(" ",
                               new[] { u.FirstName, u.LastName }
                               .Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } full
                           ? full
                           : u.Username,
            enabled = u.Enabled,
            role    = u.Role,
        });

        return Ok(new { data });
    }

    // PUT /admin/users/{keycloakId}/role
    /*[HttpPut("users/{keycloakId}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(
        string keycloakId,
        [FromBody] AssignRoleRequestDto dto,
        CancellationToken cancellationToken)
    {
        var roleName = dto.Role.ToString().ToLowerInvariant();
        await _keycloak.AssignRealmRoleAsync(keycloakId, roleName);

        return Ok(new { message = $"Rôle '{roleName}' assigné à l'utilisateur {keycloakId}." });
    }*/

    // PUT /admin/users/{keycloakId}/enabled
    [HttpPut("users/{keycloakId}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetEnabled(
        string keycloakId,
        [FromBody] SetUserEnabledRequestDto dto,
        CancellationToken cancellationToken)
    {
        await _keycloak.SetUserEnabledAsync(keycloakId, dto.Enabled);

        return Ok(new
        {
            message = dto.Enabled ? "Utilisateur activé." : "Utilisateur désactivé."
        });
    }
}

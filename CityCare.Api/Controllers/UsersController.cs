using CityCare.Api.Dtos.Users;
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
    private readonly CurrentUserService _currentUser;

    public UsersController(CityCareDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserMeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var dto = new UserMeResponseDTO(
            user.Id,
            user.KeycloakId,
            new DateTimeOffset(DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            new DateTimeOffset(DateTime.SpecifyKind(user.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)));

        return Ok(dto);
    }

    // ─────────────────────────────────────────────────────────────
    // PATCH /users/me/push-token — Enregistre le token Expo Push du device
    // ─────────────────────────────────────────────────────────────
    [HttpPatch("me/push-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePushToken(
        [FromBody] UpdatePushTokenRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        if (string.IsNullOrWhiteSpace(request.PushToken))
            return UnprocessableEntity(new { error = "push_token ne peut pas être vide." });

        user.DevicePushToken = request.PushToken.Trim();
        user.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var incidentsRaw = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.AuthorUserId == user.Id)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                id = i.Id,
                type = i.Type.ToString(),
                status = IncidentService.ToSnakeCase(i.Status),
                address_label = i.AddressLabel,
                created_at = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var incidents = incidentsRaw.Select(i => new
        {
            id = i.id,
            type = i.type,
            status = i.status,
            address_label = i.address_label,
            created_at = new DateTimeOffset(DateTime.SpecifyKind(i.created_at, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
        }).ToList();

        return Ok(new { data = incidents });
    }
}
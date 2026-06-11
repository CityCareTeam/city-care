using CityCare.Api.Dtos.Users;
using CityCare.Api.Services;
using CityCare.Core.Entities;
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
    private readonly KeycloakService _keycloak;

    public UsersController(CityCareDbContext db, CurrentUserService currentUser, KeycloakService keycloak)
    {
        _db = db;
        _currentUser = currentUser;
        _keycloak = keycloak;
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

    [HttpPatch("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        if (dto.Email != null || dto.Username != null || dto.FirstName != null || dto.LastName != null)
            await _keycloak.UpdateUserAsync(user.KeycloakId, dto.Email, dto.Username, dto.FirstName, dto.LastName);

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            await _keycloak.UpdatePasswordAsync(user.KeycloakId, dto.NewPassword);

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Profil mis à jour avec succès." });
    }

    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        // Supprimer d'abord les références Restrict avant la suppression en cascade
        await _db.Set<IncidentStatusHistory>()
            .Where(h => h.ChangedByUserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Set<IncidentPhoto>()
            .Where(p => p.UploadedByUserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _keycloak.DeleteUserAsync(user.KeycloakId);

        return NoContent();
    }
}
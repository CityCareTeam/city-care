using CityCare.Api.Dtos.Notifications;
using CityCare.Api.Services;
using CityCare.Core.Entities;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

/// Lot 3 — Préférences de notification de l'utilisateur connecté.
/// Contrôleur séparé pour la sous-ressource <c>/users/me/notification-settings</c>
/// (l'UsersController existant n'est pas modifié).
[ApiController]
[Route("users/me/notification-settings")]
[Authorize]
public sealed class UserNotificationSettingsController : ControllerBase
{
    private readonly CityCareDbContext _db;
    private readonly CurrentUserService _currentUser;

    public UserNotificationSettingsController(CityCareDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /users/me/notification-settings — Préférences de l'utilisateur connecté
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(typeof(NotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var settings = await GetOrCreateAsync(user.Id, cancellationToken);
        return Ok(NotificationSettingsResponse.From(settings));
    }

    // ─────────────────────────────────────────────────────────────
    // PATCH /users/me/notification-settings — Mise à jour partielle des préférences
    // ─────────────────────────────────────────────────────────────
    [HttpPatch]
    [ProducesResponseType(typeof(NotificationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Patch(
        [FromBody] UpdateNotificationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        // Validation + normalisation des types suivis (si fournis).
        string? followedTypesCsv = null;
        if (request.FollowedIncidentTypes is not null)
        {
            var normalized = new List<string>();
            foreach (var raw in request.FollowedIncidentTypes)
            {
                if (!IncidentService.TryParseTypeSnakeCase(raw, out var parsedType))
                    return UnprocessableEntity(new
                    {
                        error = $"Type d'incident invalide: '{raw}'. Valeurs attendues: road, lighting, waste, graffiti, safety, other."
                    });

                normalized.Add(IncidentService.ToSnakeCase(parsedType));
            }

            followedTypesCsv = string.Join(',', normalized.Distinct());
        }

        var settings = await GetOrCreateAsync(user.Id, cancellationToken);

        if (request.EmailEnabled.HasValue) settings.EmailEnabled = request.EmailEnabled.Value;
        if (request.PushEnabled.HasValue) settings.PushEnabled = request.PushEnabled.Value;
        if (followedTypesCsv is not null) settings.FollowedTypes = followedTypesCsv;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(NotificationSettingsResponse.From(settings));
    }

    /// Récupère les préférences de l'utilisateur, en créant une ligne par défaut
    /// au premier accès (même logique que CurrentUserService pour les users).
    private async Task<UserNotificationSettings> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is not null)
            return settings;

        var now = DateTime.UtcNow;
        settings = new UserNotificationSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EmailEnabled = true,
            PushEnabled = true,
            // Par défaut : tous les types suivis.
            FollowedTypes = string.Join(',', Enum.GetValues<IncidentType>().Select(IncidentService.ToSnakeCase)),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.UserNotificationSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
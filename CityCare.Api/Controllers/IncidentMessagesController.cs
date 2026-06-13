using System.Security.Claims;
using CityCare.Api.Dtos.Messages;
using CityCare.Api.Hubs;
using CityCare.Api.Services;
using CityCare.Core.Entities;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

/// Lot 2 — Fil de discussion d'un incident.
/// Sous-ressource <c>/incidents/{id}/messages</c>.
/// Après chaque POST, le message est broadcasté en temps réel via SignalR
/// à tous les clients connectés au groupe "incident-{id}".
[ApiController]
[Route("incidents/{incidentId:guid}/messages")]
[Authorize]
public sealed class IncidentMessagesController : ControllerBase
{
    private readonly CityCareDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly IHubContext<IncidentChatHub> _hub;
    private readonly NotificationService _notificationService;
    private readonly ExpoPushService _expoPush;

    public IncidentMessagesController(
        CityCareDbContext db,
        CurrentUserService currentUser,
        IHubContext<IncidentChatHub> hub,
        NotificationService notificationService,
        ExpoPushService expoPush)
    {
        _db = db;
        _currentUser = currentUser;
        _hub = hub;
        _notificationService = notificationService;
        _expoPush = expoPush;
    }

    // ─────────────────────────────────────────────────────────────
    // POST /incidents/{id}/messages — Poster un message (citoyen ou agent)
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Post(
        Guid incidentId,
        [FromBody] CreateMessageRequest request,
        CancellationToken cancellationToken)
    {
        var author = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (author is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var incident = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident is null)
            return NotFound(new { error = "Incident introuvable." });

        // Un citoyen ne peut poster que sur son propre signalement.
        var role = ResolveRole(User);
        if (role is "citizen" && incident.AuthorUserId != author.Id)
            return Forbid();

        var message = new IncidentMessage
        {
            Id           = Guid.NewGuid(),
            IncidentId   = incidentId,
            AuthorUserId = author.Id,
            AuthorRole   = role,
            AuthorName   = author.DisplayName,
            Content      = request.Content,
            CreatedAt    = DateTime.UtcNow
        };

        _db.IncidentMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var response = MessageResponse.From(message);

        // Broadcast temps réel à tous les clients connectés au fil de cet incident
        await _hub.Clients
            .Group(IncidentChatHub.GroupName(incidentId))
            .SendAsync("ReceiveMessage", response, cancellationToken);

        // Routing basé sur author.MainRole (synced depuis Keycloak via DB),
        // pas sur le JWT live qui peut contenir plusieurs rôles (ex. compte test citizen+agent).
        var notifPushTitle = "Nouveau message";
        var notifPushBody  = $"Nouveau message sur le signalement : {incident.AddressLabel}";

        if (author.MainRole is "citizen")
        {
            // Citoyen a écrit → notifier tous les agents/admins.
            var agents = await (
                from u in _db.Users
                where (u.MainRole == "agent" || u.MainRole == "admin") && u.Id != author.Id
                join s in _db.UserNotificationSettings on u.Id equals s.UserId into settings
                from s in settings.DefaultIfEmpty()
                select new
                {
                    u.Id,
                    u.DevicePushToken,
                    InAppOk = s == null || s.InAppMessagesEnabled,
                    PushOk  = u.DevicePushToken != null && (s == null || s.PushMessagesEnabled)
                }
            ).ToListAsync(cancellationToken);

            var inAppIds = agents.Where(a => a.InAppOk).Select(a => a.Id).ToList();
            if (inAppIds.Count > 0)
                await _notificationService.UpsertMessageNotifBulkAsync(
                    inAppIds, addressLabel: incident.AddressLabel,
                    incidentId: incident.Id, cancellationToken: cancellationToken);

            var pushTokens = agents.Where(a => a.PushOk).Select(a => a.DevicePushToken!).ToList();
            if (pushTokens.Count > 0)
                _ = _expoPush.SendBatchAsync(pushTokens, notifPushTitle, notifPushBody,
                    data: new { incident_id = incident.Id });
        }
        else
        {
            // Agent/admin a écrit → notifier le citoyen auteur de l'incident (pas l'expéditeur lui-même).
            if (incident.AuthorUserId != author.Id)
            {
                var citizenPrefs = await _db.UserNotificationSettings
                    .AsNoTracking()
                    .Where(s => s.UserId == incident.AuthorUserId)
                    .Select(s => new { s.InAppMessagesEnabled, s.PushMessagesEnabled })
                    .FirstOrDefaultAsync(cancellationToken);

                var inAppOk = citizenPrefs?.InAppMessagesEnabled ?? true;
                var pushOk  = citizenPrefs?.PushMessagesEnabled  ?? true;

                if (inAppOk)
                    await _notificationService.UpsertMessageNotifAsync(
                        userId: incident.AuthorUserId,
                        addressLabel: incident.AddressLabel,
                        incidentId: incident.Id,
                        cancellationToken: cancellationToken);

                if (pushOk)
                {
                    var citizenToken = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == incident.AuthorUserId)
                        .Select(u => u.DevicePushToken)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(citizenToken))
                        _ = _expoPush.SendAsync(citizenToken, notifPushTitle, notifPushBody,
                            data: new { incident_id = incident.Id });
                }
            }
        }

        return CreatedAtAction(nameof(GetAll), new { incidentId }, response);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/{id}/messages — Lister les messages (auteur, contenu, date)
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid incidentId, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (currentUser is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var incident = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident is null)
            return NotFound(new { error = "Incident introuvable." });

        // Un citoyen ne peut lire que les messages de son propre signalement.
        var role = ResolveRole(User);
        if (role is "citizen" && incident.AuthorUserId != currentUser.Id)
            return Forbid();

        var messages = await _db.IncidentMessages
            .AsNoTracking()
            .Where(m => m.IncidentId == incidentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new { data = messages.Select(MessageResponse.From).ToList() });
    }

    /// Résout le rôle applicatif de l'auteur depuis les claims Keycloak.
    /// Priorité : claim "mainRole" (valeur explicite Keycloak) > IsInRole.
    private static string? ResolveRole(ClaimsPrincipal user) =>
        new[] { "admin", "agent", "citizen" }.FirstOrDefault(user.IsInRole);
}
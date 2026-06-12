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

    public IncidentMessagesController(
        CityCareDbContext db,
        CurrentUserService currentUser,
        IHubContext<IncidentChatHub> hub)
    {
        _db = db;
        _currentUser = currentUser;
        _hub = hub;
    }

    // ─────────────────────────────────────────────────────────────
    // POST /incidents/{id}/messages — Poster un message (citoyen ou agent)
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Post(
        Guid incidentId,
        [FromBody] CreateMessageRequest request,
        CancellationToken cancellationToken)
    {
        var incidentExists = await _db.Incidents
            .AsNoTracking()
            .AnyAsync(i => i.Id == incidentId, cancellationToken);

        if (!incidentExists)
            return NotFound(new { error = "Incident introuvable." });

        var author = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (author is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var message = new IncidentMessage
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            AuthorUserId = author.Id,
            AuthorRole = ResolveRole(User),
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _db.IncidentMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var response = MessageResponse.From(message);

        // Broadcast temps réel à tous les clients connectés au fil de cet incident
        await _hub.Clients
            .Group(IncidentChatHub.GroupName(incidentId))
            .SendAsync("ReceiveMessage", response, cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { incidentId }, response);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/{id}/messages — Lister les messages (auteur, contenu, date)
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid incidentId, CancellationToken cancellationToken)
    {
        var incidentExists = await _db.Incidents
            .AsNoTracking()
            .AnyAsync(i => i.Id == incidentId, cancellationToken);

        if (!incidentExists)
            return NotFound(new { error = "Incident introuvable." });

        var messages = await _db.IncidentMessages
            .AsNoTracking()
            .Where(m => m.IncidentId == incidentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new { data = messages.Select(MessageResponse.From).ToList() });
    }

    /// Résout le rôle applicatif de l'auteur depuis les claims Keycloak.
    private static string? ResolveRole(ClaimsPrincipal user)
    {
        foreach (var role in new[] { "admin", "agent", "citizen" })
        {
            if (user.IsInRole(role))
                return role;
        }

        return user.FindFirst("mainRole")?.Value?.ToLowerInvariant();
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CityCare.Api.Hubs;

/// Hub SignalR pour le chat temps réel lié à un incident.
/// Chaque incident a son propre groupe : "incident-{id}".
/// Le client rejoint le groupe à la connexion en appelant JoinIncident,
/// et quitte avec LeaveIncident.

[Authorize]
public sealed class IncidentChatHub : Hub
{
    // ─────────────────────────────────────────────────────────────
    // Appelé par le client pour rejoindre le fil d'un incident
    // ─────────────────────────────────────────────────────────────
    public async Task JoinIncident(Guid incidentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(incidentId));
    }

    // ─────────────────────────────────────────────────────────────
    // Appelé par le client pour quitter le fil
    // ─────────────────────────────────────────────────────────────
    public async Task LeaveIncident(Guid incidentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(incidentId));
    }

    public static string GroupName(Guid incidentId) => $"incident-{incidentId}";
}
namespace CityCare.Core.Entities;


/// Message posté sur un incident (citoyen ou agent) — Lot 2.
/// Mappé sur la table <c>incident_messages</c> (config dans CityCareDbContext).

/// <see cref="AuthorRole"/> est dénormalisé à l'insertion pour afficher l'auteur
/// (« citizen » / « agent ») en lecture sans jointure supplémentaire.

public class IncidentMessage
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Guid AuthorUserId { get; set; }

    /// Rôle de l'auteur au moment du post : citizen / agent / admin.
    public string? AuthorRole { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
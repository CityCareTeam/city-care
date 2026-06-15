namespace CityCare.Core.Entities;


/// Préférences de notification d'un utilisateur (Lot 3).
/// Mappé sur la table <c>user_notification_settings</c> (config dans CityCareDbContext),
/// une ligne par utilisateur (relation 1-1 avec <c>users</c>).

/// <see cref="FollowedTypes"/> stocke les types d'incidents suivis sous forme de
/// liste CSV de jetons snake_case (ex. <c>"road,waste,safety"</c>), validée côté API
/// via <c>IncidentService.TryParseTypeSnakeCase</c>.

public class UserNotificationSettings
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool PushEnabled { get; set; } = true;

    /// Notifs in-app pour les signalements (nouveau + changement de statut) (défaut : activé).
    public bool InAppIncidentsEnabled { get; set; } = true;

    /// Notifs in-app pour les nouveaux messages (défaut : activé).
    public bool InAppMessagesEnabled { get; set; } = true;

    /// Notifs push pour les nouveaux messages (défaut : activé).
    public bool PushMessagesEnabled { get; set; } = true;

    /// Types d'incidents suivis, en CSV snake_case. Vide = aucun.
    public string FollowedTypes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
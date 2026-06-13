namespace CityCare.Core.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Titre court affiché dans le centre de notifications.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Corps du message.</summary>
    public string Body { get; set; } = null!;

    /// <summary>Type d'événement snake_case (ex. "incident_status_changed").</summary>
    public string Type { get; set; } = null!;

    /// <summary>Référence optionnelle vers l'incident concerné.</summary>
    public Guid? IncidentId { get; set; }

    public bool IsRead { get; set; } = false;

    /// Nombre de messages groupés (uniquement pour type "new_message", null sinon).
    public int? MessageCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

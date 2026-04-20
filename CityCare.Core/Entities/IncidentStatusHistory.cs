using CityCare.Core.Enums;

namespace CityCare.Core.Entities;

public class IncidentStatusHistory
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;

    public IncidentStatus OldStatus { get; set; }
    public IncidentStatus NewStatus { get; set; }

    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;

    public string? Comment { get; set; }
    public DateTime ChangedAt { get; set; }
}

using CityCare.Core.Enums;

namespace CityCare.Core.Entities;

public class Incident
{
    public Guid Id { get; set; }

    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    public IncidentType Type { get; set; }
    public string Description { get; set; } = null!;

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string AddressLabel { get; set; } = null!;

    public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
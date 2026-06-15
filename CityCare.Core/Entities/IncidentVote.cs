namespace CityCare.Core.Entities;

public class IncidentVote
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

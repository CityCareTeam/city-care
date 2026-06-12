namespace CityCare.Core.Entities;

public class User
{
    public Guid Id { get; set; }

    public string KeycloakId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
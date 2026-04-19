using CityCare.Core.Enums;

namespace CityCare.Core.Entities;

public class User
{
    public Guid Id { get; set; }

    public string KeycloakId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.Citizen;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
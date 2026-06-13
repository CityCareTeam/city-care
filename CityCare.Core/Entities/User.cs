namespace CityCare.Core.Entities;

public class User
{
    public Guid Id { get; set; }

    public string KeycloakId { get; set; } = null!;

    /// <summary>Rôle principal synced depuis le claim Keycloak "mainRole" à chaque connexion.</summary>
    public string? MainRole { get; set; }

    /// <summary>Token Expo Push — null tant que l'app mobile n'a pas appelé PATCH /users/me/push-token.</summary>
    public string? DevicePushToken { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
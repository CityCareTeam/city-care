using System.Text.Json.Serialization;

namespace CityCare.Api.Dtos.Notifications;

public sealed class UpdateNotificationSettingsRequest
{
    [JsonPropertyName("email_enabled")]
    public bool? EmailEnabled { get; set; }

    [JsonPropertyName("push_enabled")]
    public bool? PushEnabled { get; set; }

    
    /// Liste complète des types suivis en snake_case (remplace l'existante).
    /// Valeurs : road, lighting, waste, graffiti, safety, other.
    
    [JsonPropertyName("followed_incident_types")]
    public List<string>? FollowedIncidentTypes { get; set; }
}
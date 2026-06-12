using System.Text.Json.Serialization;
using CityCare.Core.Entities;

namespace CityCare.Api.Dtos.Notifications;

public sealed class NotificationSettingsResponse
{
    [JsonPropertyName("email_enabled")]
    public bool EmailEnabled { get; set; }

    [JsonPropertyName("push_enabled")]
    public bool PushEnabled { get; set; }

    [JsonPropertyName("followed_incident_types")]
    public IReadOnlyList<string> FollowedIncidentTypes { get; set; } = [];

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    public static NotificationSettingsResponse From(UserNotificationSettings s) => new()
    {
        EmailEnabled = s.EmailEnabled,
        PushEnabled = s.PushEnabled,
        FollowedIncidentTypes = string.IsNullOrWhiteSpace(s.FollowedTypes)
            ? []
            : s.FollowedTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(s.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
    };
}
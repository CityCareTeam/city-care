using System.Text.Json.Serialization;
using CityCare.Core.Entities;

namespace CityCare.Api.Dtos.Notifications;

public sealed class NotificationResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("body")]
    public string Body { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("incident_id")]
    public Guid? IncidentId { get; set; }

    [JsonPropertyName("is_read")]
    public bool IsRead { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public static NotificationResponse From(Notification n) => new()
    {
        Id         = n.Id,
        Title      = n.Title,
        Body       = n.Body,
        Type       = n.Type,
        IncidentId = n.IncidentId,
        IsRead     = n.IsRead,
        CreatedAt  = new DateTimeOffset(DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
    };
}

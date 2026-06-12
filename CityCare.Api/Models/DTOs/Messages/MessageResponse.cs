using System.Text.Json.Serialization;
using CityCare.Core.Entities;

namespace CityCare.Api.Dtos.Messages;

public sealed class MessageResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("incident_id")]
    public Guid IncidentId { get; set; }

    [JsonPropertyName("author_user_id")]
    public Guid AuthorUserId { get; set; }

    [JsonPropertyName("author_role")]
    public string? AuthorRole { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public static MessageResponse From(IncidentMessage m) => new()
    {
        Id = m.Id,
        IncidentId = m.IncidentId,
        AuthorUserId = m.AuthorUserId,
        AuthorRole = m.AuthorRole,
        Content = m.Content,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
    };
}
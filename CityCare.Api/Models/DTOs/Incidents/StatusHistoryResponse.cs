namespace CityCare.Api.Dtos.Incidents;

public class StatusHistoryResponse
{
    public Guid Id { get; set; }
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public Guid ChangedByUserId { get; set; }
    public string ChangedByKeycloakId { get; set; } = null!;
    public string? Comment { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

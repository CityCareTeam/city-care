namespace CityCare.Api.Dtos.Incidents;

public class PhotoResponse
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string Url { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

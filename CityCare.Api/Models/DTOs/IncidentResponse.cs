using System;

namespace CityCare.Api.Dtos.Incidents;

public class IncidentResponse
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Type { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string AddressLabel { get; set; } = null!;
    public string Status { get; set; } = null!;
    // Use DateTimeOffset so responses include timezone offset information
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
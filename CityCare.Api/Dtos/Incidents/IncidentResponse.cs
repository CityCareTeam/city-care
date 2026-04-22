namespace CityCare.Api.Dtos.Incidents;

public record IncidentResponse(
    Guid Id,
    Guid AuthorUserId,
    string? AuthorDisplayName,
    string Type,
    string Description,
    decimal Latitude,
    decimal Longitude,
    string AddressLabel,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt
);

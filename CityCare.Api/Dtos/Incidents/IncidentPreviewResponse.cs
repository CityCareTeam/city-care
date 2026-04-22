namespace CityCare.Api.Dtos.Incidents;

public record IncidentPreviewResponse(
    Guid Id,
    string Type,
    string Status,
    decimal Latitude,
    decimal Longitude,
    string AddressLabel,
    DateTime CreatedAt
);

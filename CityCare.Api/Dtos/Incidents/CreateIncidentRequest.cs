namespace CityCare.Api.Dtos.Incidents;

public record CreateIncidentRequest(
    string Type,
    string Description,
    decimal Latitude,
    decimal Longitude,
    string? AddressLabel
);

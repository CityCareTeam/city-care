namespace CityCare.Api.Dtos.Incidents;

public class IncidentPreviewResponse
{
    // Identifiant de l'incident
    public Guid Id { get; set; }

    // Coordonnées GPS brutes
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    // Adresse enrichie via Nominatim
    public string AddressLabel { get; set; } = null!;
    public string? City { get; set; }
    public string? Postcode { get; set; }
    public string? Country { get; set; }
}
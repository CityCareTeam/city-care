using CityCare.Core.Enums;

namespace CityCare.Api.Dtos.Incidents;

public class CreateIncidentRequest
{
    public IncidentType Type { get; set; }
    public string Description { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
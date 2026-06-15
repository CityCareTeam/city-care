using System.ComponentModel.DataAnnotations;
using CityCare.Core.Enums;

namespace CityCare.Api.Dtos.Incidents;

public class CreateIncidentRequest
{
    public IncidentType Type { get; set; }

    [Required]
    [MaxLength(255, ErrorMessage = "La description ne peut pas dépasser 255 caractères.")]
    public string Description { get; set; } = null!;

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
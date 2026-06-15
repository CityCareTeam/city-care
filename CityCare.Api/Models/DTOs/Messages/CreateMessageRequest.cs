using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CityCare.Api.Dtos.Messages;

/// Corps de requête pour poster un message sur un incident.
public sealed class CreateMessageRequest
{
    [Required(ErrorMessage = "Le contenu du message est requis.")]
    [MaxLength(2000, ErrorMessage = "Le message ne peut pas dépasser 2000 caractères.")]
    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}
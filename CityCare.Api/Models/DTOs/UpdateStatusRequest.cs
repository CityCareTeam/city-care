using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CityCare.Api.Models.DTOs;

public sealed class UpdateStatusRequest
{
    [Required]
    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }
}

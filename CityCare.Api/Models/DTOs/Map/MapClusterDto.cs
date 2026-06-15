using System.Text.Json.Serialization;

namespace CityCare.Api.Dtos.Map;

public sealed record MapClusterDto(
    [property: JsonPropertyName("latitude")] decimal Latitude,
    [property: JsonPropertyName("longitude")] decimal Longitude,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("reported")] int Reported,
    [property: JsonPropertyName("in_progress")] int InProgress,
    [property: JsonPropertyName("resolved")] int Resolved);
using System.Text.Json.Serialization;

namespace CityCare.Api.Dtos.Map;

public sealed record MapSummaryResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<MapClusterDto> Data,
    [property: JsonPropertyName("cell_size")] decimal CellSize,
    [property: JsonPropertyName("total")] int Total);
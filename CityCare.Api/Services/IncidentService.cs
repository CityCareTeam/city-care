using CityCare.Core.Enums;

namespace CityCare.Api.Services;

public sealed class IncidentService
{
    public bool IsValidTransition(IncidentStatus current, IncidentStatus next) =>
        (current, next) switch
        {
            (IncidentStatus.Reported, IncidentStatus.InProgress) => true,
            (IncidentStatus.InProgress, IncidentStatus.Resolved) => true,
            _ => false
        };

    // --- IncidentStatus ---

    public static string ToSnakeCase(IncidentStatus status) =>
        status switch
        {
            IncidentStatus.Reported   => "reported",
            IncidentStatus.InProgress => "in_progress",
            IncidentStatus.Resolved   => "resolved",
            _ => status.ToString()
        };

    public static bool TryParseSnakeCase(string value, out IncidentStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "reported"    => (status = IncidentStatus.Reported)   == IncidentStatus.Reported,
            "in_progress" => (status = IncidentStatus.InProgress) == IncidentStatus.InProgress,
            "resolved"    => (status = IncidentStatus.Resolved)   == IncidentStatus.Resolved,
            _ => false
        };
    }

    // --- IncidentType (AJOUT) ---

    public static string ToSnakeCase(IncidentType type) =>
        type switch
        {
            IncidentType.Road     => "road",
            IncidentType.Lighting => "lighting",
            IncidentType.Waste    => "waste",
            IncidentType.Graffiti => "graffiti",
            IncidentType.Safety   => "safety",
            IncidentType.Other    => "other",
            _ => type.ToString()
        };

    public static bool TryParseTypeSnakeCase(string value, out IncidentType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "road"     => (type = IncidentType.Road)     == IncidentType.Road,
            "lighting" => (type = IncidentType.Lighting) == IncidentType.Lighting,
            "waste"    => (type = IncidentType.Waste)    == IncidentType.Waste,
            "graffiti" => (type = IncidentType.Graffiti) == IncidentType.Graffiti,
            "safety"   => (type = IncidentType.Safety)   == IncidentType.Safety,
            "other"    => (type = IncidentType.Other)    == IncidentType.Other,
            _ => false
        };
    }
}
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

    public static string ToSnakeCase(IncidentStatus status) =>
        status switch
        {
            IncidentStatus.Reported => "reported",
            IncidentStatus.InProgress => "in_progress",
            IncidentStatus.Resolved => "resolved",
            _ => status.ToString()
        };

    public static bool TryParseSnakeCase(string value, out IncidentStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "reported" => (status = IncidentStatus.Reported) == IncidentStatus.Reported,
            "in_progress" => (status = IncidentStatus.InProgress) == IncidentStatus.InProgress,
            "resolved" => (status = IncidentStatus.Resolved) == IncidentStatus.Resolved,
            _ => false
        };
    }
}

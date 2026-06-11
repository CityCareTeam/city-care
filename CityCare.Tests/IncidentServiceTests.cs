using CityCare.Api.Services;
using CityCare.Core.Enums;

namespace CityCare.Tests;

public class IncidentServiceTests
{
    private readonly IncidentService _service = new();

    // ─────────────────────────────────────────────────────────────
    // Transitions de statut valides
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidTransition_ReportedToInProgress_ReturnsTrue()
    {
        var result = _service.IsValidTransition(IncidentStatus.Reported, IncidentStatus.InProgress);
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_InProgressToResolved_ReturnsTrue()
    {
        var result = _service.IsValidTransition(IncidentStatus.InProgress, IncidentStatus.Resolved);
        Assert.True(result);
    }

    // ─────────────────────────────────────────────────────────────
    // Transitions de statut invalides
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(IncidentStatus.Reported, IncidentStatus.Resolved)]   // saute une étape
    [InlineData(IncidentStatus.Resolved, IncidentStatus.Reported)]   // retour arrière
    [InlineData(IncidentStatus.InProgress, IncidentStatus.Reported)] // retour arrière
    [InlineData(IncidentStatus.Reported, IncidentStatus.Reported)]   // pas de changement
    public void IsValidTransition_InvalidTransitions_ReturnsFalse(IncidentStatus from, IncidentStatus to)
    {
        var result = _service.IsValidTransition(from, to);
        Assert.False(result);
    }

    // ─────────────────────────────────────────────────────────────
    // Conversion statut → snake_case
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(IncidentStatus.Reported, "reported")]
    [InlineData(IncidentStatus.InProgress, "in_progress")]
    [InlineData(IncidentStatus.Resolved, "resolved")]
    public void ToSnakeCase_Status_ReturnsExpectedString(IncidentStatus status, string expected)
    {
        var result = IncidentService.ToSnakeCase(status);
        Assert.Equal(expected, result);
    }

    // ─────────────────────────────────────────────────────────────
    // Parsing snake_case → statut
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void TryParseSnakeCase_ValidValue_ReturnsTrueAndCorrectStatus()
    {
        var ok = IncidentService.TryParseSnakeCase("in_progress", out var status);

        Assert.True(ok);
        Assert.Equal(IncidentStatus.InProgress, status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("done")]
    public void TryParseSnakeCase_InvalidValue_ReturnsFalse(string value)
    {
        var ok = IncidentService.TryParseSnakeCase(value, out _);
        Assert.False(ok);
    }

    // ─────────────────────────────────────────────────────────────
    // Conversion type d'incident → snake_case
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(IncidentType.Road, "road")]
    [InlineData(IncidentType.Lighting, "lighting")]
    [InlineData(IncidentType.Waste, "waste")]
    [InlineData(IncidentType.Graffiti, "graffiti")]
    [InlineData(IncidentType.Safety, "safety")]
    [InlineData(IncidentType.Other, "other")]
    public void ToSnakeCase_Type_ReturnsExpectedString(IncidentType type, string expected)
    {
        var result = IncidentService.ToSnakeCase(type);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseTypeSnakeCase_IsCaseInsensitive()
    {
        var ok = IncidentService.TryParseTypeSnakeCase("ROAD", out var type);

        Assert.True(ok);
        Assert.Equal(IncidentType.Road, type);
    }
}

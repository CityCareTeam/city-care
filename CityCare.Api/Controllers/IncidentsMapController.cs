using CityCare.Api.Dtos.Map;
using CityCare.Api.Services;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

/// Lot 2 — Résumé carte (clusters d'incidents par zone géographique).
/// Contrôleur séparé partageant le préfixe <c>/incidents</c> sans modifier
/// l'IncidentsController existant ; le segment littéral <c>map-summary</c>
/// n'entre pas en conflit avec la route <c>{id:guid}</c>.
[ApiController]
[Route("incidents")]
[Authorize]
public sealed class IncidentsMapController : ControllerBase
{
    private readonly CityCareDbContext _db;

    public IncidentsMapController(CityCareDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/map-summary — Clusters d'incidents pour le dézoom carte
    // ─────────────────────────────────────────────────────────────
    [HttpGet("map-summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MapSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMapSummary(
        [FromQuery] int? zoom,
        [FromQuery] decimal? cellSize,
        [FromQuery] decimal? latMin,
        [FromQuery] decimal? latMax,
        [FromQuery] decimal? lngMin,
        [FromQuery] decimal? lngMax,
        [FromQuery] string? status,
        [FromQuery] string? type,
        CancellationToken cancellationToken = default)
    {
        var size = GeoClusteringService.ResolveCellSize(zoom, cellSize);

        var query = _db.Incidents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!IncidentService.TryParseSnakeCase(status, out var parsedStatus))
                return BadRequest(new { error = "Statut invalide. Valeurs attendues: reported, in_progress, resolved." });
            query = query.Where(i => i.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!IncidentService.TryParseTypeSnakeCase(type, out var parsedType))
                return BadRequest(new { error = "Type invalide. Valeurs attendues: road, lighting, waste, graffiti, safety, other." });
            query = query.Where(i => i.Type == parsedType);
        }

        if (latMin.HasValue) query = query.Where(i => i.Latitude >= latMin.Value);
        if (latMax.HasValue) query = query.Where(i => i.Latitude <= latMax.Value);
        if (lngMin.HasValue) query = query.Where(i => i.Longitude >= lngMin.Value);
        if (lngMax.HasValue) query = query.Where(i => i.Longitude <= lngMax.Value);

        var points = await query
            .Select(i => new { i.Latitude, i.Longitude, i.Status })
            .ToListAsync(cancellationToken);

        var clusters = GeoClusteringService.Cluster(
            points.Select(p => (p.Latitude, p.Longitude, p.Status)),
            size);

        return Ok(new MapSummaryResponse(clusters, size, points.Count));
    }
}
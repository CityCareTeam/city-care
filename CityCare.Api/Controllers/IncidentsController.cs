using CityCare.Api.Dtos.Incidents;
using CityCare.Api.Models.DTOs;
using CityCare.Api.Services;
using CityCare.Core.Entities;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("incidents")]
[Authorize]
public sealed class IncidentsController : ControllerBase
{
    private readonly CityCareDbContext _db;
    private readonly IncidentService _incidentService;
    private readonly CurrentUserService _currentUser;
    private readonly GeocodeService _geocodeService;

    public IncidentsController(
        CityCareDbContext db,
        IncidentService incidentService,
        CurrentUserService currentUser,
        GeocodeService geocodeService)
    {
        _db = db;
        _incidentService = incidentService;
        _currentUser = currentUser;
        _geocodeService = geocodeService;
    }

    // ─────────────────────────────────────────────────────────────
    // POST /incidents — Créer un signalement
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var author = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (author is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var authorUserId = author.Id;

        var geocode = await _geocodeService.ReverseGeocodeAsync(
            request.Latitude, request.Longitude, cancellationToken);

        if (geocode is null)
            return UnprocessableEntity(new { error = "Impossible de résoudre les coordonnées en adresse." });

        var now = DateTime.UtcNow;

        var incident = new Incident
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = authorUserId,
            Type         = request.Type,
            Description  = request.Description,
            Latitude     = request.Latitude,
            Longitude    = request.Longitude,
            AddressLabel = geocode.AddressLabel,
            Status       = IncidentStatus.Reported,
            CreatedAt    = now,
            UpdatedAt    = now
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new IncidentResponse
        {
            Id                = incident.Id,
            AuthorUserId      = incident.AuthorUserId,
            Type              = incident.Type.ToString(),
            Description       = incident.Description,
            Latitude          = incident.Latitude,
            Longitude         = incident.Longitude,
            AddressLabel      = incident.AddressLabel,
            Status            = IncidentService.ToSnakeCase(incident.Status),
            // Convert stored UTC DateTime to DateTimeOffset with +02:00 so clients see local time
            CreatedAt         = new DateTimeOffset(DateTime.SpecifyKind(incident.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            UpdatedAt         = new DateTimeOffset(DateTime.SpecifyKind(incident.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            ResolvedAt        = incident.ResolvedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(incident.ResolvedAt.Value, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
                : null
        };

        return CreatedAtAction(nameof(GetById), new { id = incident.Id }, response);
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents — Lister avec filtres + pagination
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] decimal? latMin,
        [FromQuery] decimal? latMax,
        [FromQuery] decimal? lngMin,
        [FromQuery] decimal? lngMax,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.Incidents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!IncidentService.TryParseSnakeCase(status, out var parsedStatus))
                return BadRequest(new { error = "Statut invalide. Valeurs attendues: reported, in_progress, resolved." });
            query = query.Where(i => i.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<IncidentType>(type, ignoreCase: true, out var parsedType))
                return BadRequest(new { error = $"Type invalide. Valeurs attendues: {string.Join(", ", Enum.GetNames<IncidentType>())}." });
            query = query.Where(i => i.Type == parsedType);
        }

        if (latMin.HasValue) query = query.Where(i => i.Latitude  >= latMin.Value);
        if (latMax.HasValue) query = query.Where(i => i.Latitude  <= latMax.Value);
        if (lngMin.HasValue) query = query.Where(i => i.Longitude >= lngMin.Value);
        if (lngMax.HasValue) query = query.Where(i => i.Longitude <= lngMax.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var data = items.Select(i => new IncidentResponse
        {
            Id                = i.Id,
            AuthorUserId      = i.AuthorUserId,
            Type              = i.Type.ToString(),
            Description       = i.Description,
            Latitude          = i.Latitude,
            Longitude         = i.Longitude,
            AddressLabel      = i.AddressLabel,
            Status            = IncidentService.ToSnakeCase(i.Status),
            CreatedAt         = new DateTimeOffset(DateTime.SpecifyKind(i.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            UpdatedAt         = new DateTimeOffset(DateTime.SpecifyKind(i.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            ResolvedAt        = i.ResolvedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(i.ResolvedAt.Value, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
                : null
        }).ToList();

        return Ok(new
        {
            data,
            pagination = new
            {
                page,
                page_size    = pageSize,
                total_count  = totalCount,
                total_pages  = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/{id} — Détail complet
    // ─────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (incident is null)
            return NotFound();

        return Ok(new IncidentResponse
        {
            Id                = incident.Id,
            AuthorUserId      = incident.AuthorUserId,
            Type              = incident.Type.ToString(),
            Description       = incident.Description,
            Latitude          = incident.Latitude,
            Longitude         = incident.Longitude,
            AddressLabel      = incident.AddressLabel,
            Status            = IncidentService.ToSnakeCase(incident.Status),
            CreatedAt         = new DateTimeOffset(DateTime.SpecifyKind(incident.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            UpdatedAt         = new DateTimeOffset(DateTime.SpecifyKind(incident.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2)),
            ResolvedAt        = incident.ResolvedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(incident.ResolvedAt.Value, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
                : null
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/{id}/preview — Pin sur la carte via Nominatim
    // ─────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/preview")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPreview(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.Id, i.Latitude, i.Longitude })
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
            return NotFound();

        var geocode = await _geocodeService.ReverseGeocodeAsync(
            incident.Latitude, incident.Longitude, cancellationToken);

        if (geocode is null)
            return UnprocessableEntity(new { error = "Impossible de résoudre les coordonnées en adresse." });

        return Ok(new IncidentPreviewResponse
        {
            Id           = incident.Id,
            Latitude     = incident.Latitude,
            Longitude    = incident.Longitude,
            AddressLabel = geocode.AddressLabel,
            City         = geocode.City,
            Postcode     = geocode.Postcode,
            Country      = geocode.Country
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /incidents/{id}/status-history — Historique des changements de statut
    // ─────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/status-history")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatusHistory(Guid id, CancellationToken cancellationToken)
    {
        var incidentExists = await _db.Incidents
            .AsNoTracking()
            .AnyAsync(i => i.Id == id, cancellationToken);

        if (!incidentExists)
            return NotFound();

        var historyRows = await _db.IncidentStatusHistories
            .AsNoTracking()
            .Where(h => h.IncidentId == id)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new
            {
                h.Id,
                h.OldStatus,
                h.NewStatus,
                h.ChangedByUserId,
                ChangedByKeycloakId = h.ChangedByUser.KeycloakId,
                h.Comment,
                h.ChangedAt
            })
            .ToListAsync(cancellationToken);

        var history = historyRows.Select(h => new StatusHistoryResponse
        {
            Id                  = h.Id,
            OldStatus           = IncidentService.ToSnakeCase(h.OldStatus),
            NewStatus           = IncidentService.ToSnakeCase(h.NewStatus),
            ChangedByUserId     = h.ChangedByUserId,
            ChangedByKeycloakId = h.ChangedByKeycloakId,
            Comment             = h.Comment,
            ChangedAt           = new DateTimeOffset(
                DateTime.SpecifyKind(h.ChangedAt, DateTimeKind.Utc))
                .ToOffset(TimeSpan.FromHours(2))
        }).ToList();

        return Ok(new { data = history });
    }

    // ─────────────────────────────────────────────────────────────
    // PATCH /incidents/{id}/status — Changer le statut (agent/admin)
    // ─────────────────────────────────────────────────────────────
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "agent,admin")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (incident is null)
            return NotFound();

        if (!IncidentService.TryParseSnakeCase(request.Status, out var nextStatus))
            return UnprocessableEntity(new { error = "Statut invalide. Valeurs attendues: reported, in_progress, resolved." });

        if (!_incidentService.IsValidTransition(incident.Status, nextStatus))
            return UnprocessableEntity(new { error = "Transition de statut invalide." });

        var actor = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (actor is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var changedByUserId = actor.Id;

        var now           = DateTime.UtcNow;
        var currentStatus = incident.Status;

        incident.Status    = nextStatus;
        incident.UpdatedAt = now;
        if (nextStatus == IncidentStatus.Resolved)
            incident.ResolvedAt = now;

        _db.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            Id              = Guid.NewGuid(),
            IncidentId      = incident.Id,
            OldStatus       = currentStatus,
            NewStatus       = nextStatus,
            ChangedByUserId = changedByUserId,
            Comment         = request.Comment,
            ChangedAt       = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id         = incident.Id,
            status     = IncidentService.ToSnakeCase(incident.Status),
            updated_at = new DateTimeOffset(DateTime.SpecifyKind(incident.UpdatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(2))
        });
    }

    // ─────────────────────────────────────────────────────────────
    // DELETE /incidents/{id} — Supprimer un incident (admin)
    // ─────────────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (incident is null)
            return NotFound();

        _db.Incidents.Remove(incident);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
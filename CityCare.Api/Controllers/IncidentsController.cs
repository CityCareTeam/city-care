using System.Security.Claims;
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
public sealed class IncidentsController : ControllerBase
{
    private readonly CityCareDbContext _db;
    private readonly IncidentService _incidentService;

    public IncidentsController(CityCareDbContext db, IncidentService incidentService)
    {
        _db = db;
        _incidentService = incidentService;
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var authorUserId))
            return Unauthorized(new { error = "Missing or invalid user id claim." });

        if (!IncidentService.TryParseTypeSnakeCase(request.Type, out var type))
            return UnprocessableEntity(new { error = "Type invalide. Valeurs attendues: road, lighting, waste, graffiti, safety, other." });

        var now = DateTime.UtcNow;
        var incident = new Incident
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = authorUserId,
            Type         = type,
            Description  = request.Description,
            Latitude     = request.Latitude,
            Longitude    = request.Longitude,
            AddressLabel = request.AddressLabel ?? string.Empty,
            Status       = IncidentStatus.Reported,
            CreatedAt    = now,
            UpdatedAt    = now
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(incident).Reference(i => i.AuthorUser).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = incident.Id }, ToResponse(incident));
    }
    
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .Include(i => i.AuthorUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return incident is null ? NotFound() : Ok(ToResponse(incident));
    }


    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var incidents = await _db.Incidents
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IncidentPreviewResponse(
                i.Id,
                IncidentService.ToSnakeCase(i.Type),
                IncidentService.ToSnakeCase(i.Status),
                i.Latitude,
                i.Longitude,
                i.AddressLabel,
                i.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(incidents);
    }


    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "agent,admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
            return NotFound();

        if (!IncidentService.TryParseSnakeCase(request.Status, out var nextStatus))
            return UnprocessableEntity(new { error = "Statut invalide. Valeurs attendues: reported, in_progress, resolved." });

        var currentStatus = incident.Status;
        if (!_incidentService.IsValidTransition(currentStatus, nextStatus))
            return UnprocessableEntity(new { error = "Transition de statut invalide." });

        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var changedByUserId))
            return Unauthorized(new { error = "Missing or invalid user id claim." });

        var now = DateTime.UtcNow;

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
            updated_at = incident.UpdatedAt
        });
    }


    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident is null)
            return NotFound();

        _db.Incidents.Remove(incident);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }


    private static IncidentResponse ToResponse(Incident i) => new(
        i.Id,
        i.AuthorUserId,
        i.AuthorUser?.DisplayName,
        IncidentService.ToSnakeCase(i.Type),
        i.Description,
        i.Latitude,
        i.Longitude,
        i.AddressLabel,
        IncidentService.ToSnakeCase(i.Status),
        i.CreatedAt,
        i.UpdatedAt,
        i.ResolvedAt
    );
}

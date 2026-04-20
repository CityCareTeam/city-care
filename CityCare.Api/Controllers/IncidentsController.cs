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

        var changedByUserId = Guid.Parse(User.FindFirst("sub")!.Value);
        var now = DateTime.UtcNow;

        incident.Status = nextStatus;
        incident.UpdatedAt = now;
        if (nextStatus == IncidentStatus.Resolved)
            incident.ResolvedAt = now;

        _db.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            OldStatus = currentStatus,
            NewStatus = nextStatus,
            ChangedByUserId = changedByUserId,
            Comment = request.Comment,
            ChangedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = incident.Id,
            status = IncidentService.ToSnakeCase(incident.Status),
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
}

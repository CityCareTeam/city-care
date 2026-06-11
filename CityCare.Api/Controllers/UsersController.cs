using System.Security.Claims;
using CityCare.Api.Services;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersControllers : ControllerBase
{
    private readonly CityCareDbContext _db;

    public UsersControllers(CityCareDbContext db)
    {
        _db = db;
    }

    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized(new { error = "Missing or invalid user id claim." });

        var incidents = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.AuthorUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                id = i.Id,
                type = i.Type.ToString(),
                status = IncidentService.ToSnakeCase(i.Status),
                address_label = i.AddressLabel,
                created_at = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = incidents });
    }
}
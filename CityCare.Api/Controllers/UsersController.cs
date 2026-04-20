using CityCare.Api.Services;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly CityCareDbContext _db;

    public UsersController(CityCareDbContext db)
    {
        _db = db;
    }

    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);

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

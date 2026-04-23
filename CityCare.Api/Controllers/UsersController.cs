using CityCare.Api.Dtos.Users;
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
    private readonly CurrentUserService _currentUser;

    public UsersController(CityCareDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserMeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var dto = new UserMeResponseDTO(
            user.Id,
            user.KeycloakId,
            user.CreatedAt,
            user.UpdatedAt);

        return Ok(dto);
    }

    [HttpGet("me/incidents")]
    public async Task<IActionResult> GetMyIncidents(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetOrCreateFromPrincipalAsync(User, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Missing or invalid Keycloak subject (sub)." });

        var incidents = await _db.Incidents
            .AsNoTracking()
            .Where(i => i.AuthorUserId == user.Id)
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
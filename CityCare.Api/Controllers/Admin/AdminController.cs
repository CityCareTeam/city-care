using CityCare.Api.Dtos.Admin;
using CityCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityCare.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly KeycloakService _keycloak;

    public AdminController(KeycloakService keycloak)
    {
        _keycloak = keycloak;
    }

    [HttpPut("users/{keycloakId}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(string keycloakId, [FromBody] AssignRoleRequestDto dto, CancellationToken cancellationToken)
    {
        var roleName = dto.Role.ToString().ToLowerInvariant();
        await _keycloak.AssignRealmRoleAsync(keycloakId, roleName);

        return Ok(new { message = $"Rôle '{roleName}' assigné à l'utilisateur {keycloakId}." });
    }
}

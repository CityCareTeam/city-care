using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityCare.Api.Dtos.Auth;
using CityCare.Core.Enums;

namespace CityCare.Api.Controllers.Auth;

[ApiController]
[Route("auth")]
[Authorize]
public class AuthController : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthMeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthMeResponseDTO> GetMe()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var username = User.FindFirst("preferred_username")?.Value;

        var ignoredRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "offline_access",
            "default-roles-citycare",
            "uma_authorization"
        };

        // Lire les rôles depuis le claim "roles" et ClaimTypes.Role
        var roles = User.FindAll("roles")
            .Concat(User.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => !ignoredRoles.Contains(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mainRole = ResolveMainRole(roles);

        var response = new AuthMeResponseDTO(
            Sub: sub,
            Email: email,
            Username: username,
            Roles: roles,
            MainRole: mainRole
        );

        return Ok(response);
    }

    private static UserRole? ResolveMainRole(IEnumerable<string> roles)
    {
        var normalizedRoles = roles
            .Select(r => r.Trim().ToLowerInvariant())
            .ToHashSet();

        if (normalizedRoles.Contains("admin"))
            return UserRole.Admin;

        if (normalizedRoles.Contains("agent"))
            return UserRole.Agent;

        if (normalizedRoles.Contains("citizen"))
            return UserRole.Citizen;

        return null;
    }
}


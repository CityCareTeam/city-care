using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityCare.Api.Dtos.Auth;
using CityCare.Api.Services;
using CityCare.Core.Enums;

namespace CityCare.Api.Controllers.Auth;

[ApiController]
[Route("auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly KeycloakService _keycloakService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(KeycloakService keycloakService, ILogger<AuthController> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            _logger.LogInformation("Tentative d'inscription pour l'utilisateur: {Username}", request.Username);

            var userId = await _keycloakService.CreateUserAsync(
                request.Email, 
                request.Username, 
                request.FirstName,
                request.LastName,
                request.Password);

            var response = new RegisterResponseDto(
                UserId: userId,
                Email: request.Email,
                Username: request.Username,
                LastName: request.LastName,
                FirstName: request.FirstName,
                Message: "Utilisateur créé avec succès"
            );

            return CreatedAtAction(nameof(GetMe), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'inscription de l'utilisateur {Username}", request.Username);
            return BadRequest(new { message = "Erreur lors de la création de l'utilisateur", error = ex.Message });
        }
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthMeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthMeResponseDto> GetMe()
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

        var response = new AuthMeResponseDto(
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


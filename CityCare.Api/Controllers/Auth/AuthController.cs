using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityCare.Api.Dtos.Auth;
using CityCare.Api.Services;

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
        var sub = PrincipalClaimsHelper.GetKeycloakSub(User);
        var email = PrincipalClaimsHelper.GetEmail(User);
        var username = User.FindFirst("preferred_username")?.Value;
        var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;
        var roles = PrincipalClaimsHelper.GetBusinessRoles(User);
        var mainRole = PrincipalClaimsHelper.ResolveMainRole(User);

        var response = new AuthMeResponseDto(
            Sub: sub,
            Email: email,
            Username: username,
            FirstName : firstName,
            LastName : lastName,
            Roles: roles,
            MainRole: mainRole
        );

        return Ok(response);
    }

}


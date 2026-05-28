using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityCare.Api.Models.Dtos.Auth;
using CityCare.Api.Services;
using CityCare.Core.Enums;

namespace CityCare.Api.Controllers.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly KeycloakService _keycloakService;
    private readonly CurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthController(
        KeycloakService keycloakService,
        CurrentUserService currentUser,
        ILogger<AuthController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _keycloakService = keycloakService;
        _currentUser = currentUser;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterResponseDto>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Tentative d'inscription pour l'utilisateur: {Username}", request.Username);

            var keycloakUserId = await _keycloakService.CreateUserAsync(
                request.Email, 
                request.Username, 
                request.FirstName,
                request.LastName,
                request.Password);

            await _currentUser.GetOrCreateByKeycloakIdAsync(keycloakUserId, cancellationToken);

            var response = new RegisterResponseDto(
                UserId: keycloakUserId,
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
    
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthLoginResponseDto>> Login([FromBody] AuthLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required.");
        }

        var baseUrl = _configuration["Keycloak:Url"];
        var realm = _configuration["Keycloak:Realm"];
        var clientId = _configuration["Keycloak:ClientId"];

        var tokenUrl = $"{baseUrl}/realms/{realm}/protocol/openid-connect/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId!,
            ["grant_type"] = "password",
            ["username"] = request.Username,
            ["password"] = request.Password
        };

        var client = _httpClientFactory.CreateClient();

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(tokenUrl, content);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Unauthorized(json);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = root.GetProperty("refresh_token").GetString() ?? string.Empty;
        var tokenType = root.GetProperty("token_type").GetString() ?? "Bearer";
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        var dto = new AuthLoginResponseDto(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: tokenType,
            ExpiresIn: expiresIn
        );

        return Ok(dto);
    }
    
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthLoginResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Refresh token is required.");

        var baseUrl = _configuration["Keycloak:Url"];
        var realm = _configuration["Keycloak:Realm"];
        var clientId = _configuration["Keycloak:ClientId"];

        var tokenUrl = $"{baseUrl}/realms/{realm}/protocol/openid-connect/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId!,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = request.RefreshToken
        };

        var client = _httpClientFactory.CreateClient();

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(tokenUrl, content);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return Unauthorized(json);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dto = new AuthLoginResponseDto(
            AccessToken: root.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken: root.GetProperty("refresh_token").GetString() ?? string.Empty,
            TokenType: root.GetProperty("token_type").GetString() ?? "Bearer",
            ExpiresIn: root.GetProperty("expires_in").GetInt32()
        );

        return Ok(dto);
    }
    
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthMeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthMeResponseDto> GetMe()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var username = User.FindFirst("preferred_username")?.Value;
        var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;


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
            FirstName : firstName,
            LastName : lastName,
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
    
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Refresh token is required.");

        var baseUrl = _configuration["Keycloak:Url"];
        var realm = _configuration["Keycloak:Realm"];
        var clientId = _configuration["Keycloak:ClientId"];

        var logoutUrl = $"{baseUrl}/realms/{realm}/protocol/openid-connect/logout";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId!,
            ["refresh_token"] = request.RefreshToken
        };

        var client = _httpClientFactory.CreateClient();

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(logoutUrl, content);

        if (!response.IsSuccessStatusCode)
            return Unauthorized(await response.Content.ReadAsStringAsync());

        return NoContent();
    }
}


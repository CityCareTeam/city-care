using System.Text.Json;
using CityCare.Api.Dtos.Auth;
using CityCare.Api.Services;
using CityCare.Core.Entities;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly KeycloakService _keycloakService;
    private readonly CityCareDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public AuthController(
        KeycloakService keycloakService,
        CityCareDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _keycloakService = keycloakService;
        _db              = db;
        _config          = config;
        _http            = httpClientFactory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────
    // POST /auth/login — Connexion via Keycloak (tâche 1)
    // ─────────────────────────────────────────────────────────────
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] AuthLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var keycloakUrl = _config["Keycloak:Url"] ?? "http://localhost:8080";
        var realm       = _config["Keycloak:Realm"] ?? "CityCare";
        var clientId    = _config["Keycloak:ClientId"] ?? "citycare-web";

        var tokenUrl = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/token";

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id",  clientId),
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username",   request.Username),
            new KeyValuePair<string, string>("password",   request.Password),
        });

        var response = await _http.PostAsync(tokenUrl, body, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Unauthorized(new { error = "Identifiants invalides." });

        var json   = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);

        return Ok(new AuthLoginResponseDto(
            AccessToken:  parsed.GetProperty("access_token").GetString()!,
            RefreshToken: parsed.GetProperty("refresh_token").GetString()!,
            TokenType:    parsed.GetProperty("token_type").GetString()!,
            ExpiresIn:    parsed.GetProperty("expires_in").GetInt32()
        ));
    }

    // ─────────────────────────────────────────────────────────────
    // POST /auth/register — Inscription (tâche 2)
    // ─────────────────────────────────────────────────────────────
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var emailExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
            return Conflict(new { error = "Un compte avec cet e-mail existe déjà." });

        var keycloakId = await _keycloakService.CreateUserAsync(
            request.Email,
            request.Username,
            request.FirstName,
            request.LastName,
            request.Password);

        var now  = DateTime.UtcNow;
        var user = new User
        {
            Id          = Guid.NewGuid(),
            KeycloakId  = keycloakId,
            Email       = request.Email,
            DisplayName = $"{request.FirstName} {request.LastName}".Trim(),
            Role        = UserRole.Citizen,
            CreatedAt   = now,
            UpdatedAt   = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(null, null, new RegisterResponseDto(
            UserId:    keycloakId,
            Email:     request.Email,
            Username:  request.Username,
            LastName:  request.LastName,
            FirstName: request.FirstName,
            Message:   "Compte créé avec succès."
        ));
    }

    // ─────────────────────────────────────────────────────────────
    // POST /auth/logout — Déconnexion (révocation refresh token) (tâche 3)
    // ─────────────────────────────────────────────────────────────
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequestDto request,
        CancellationToken cancellationToken)
    {
        var keycloakUrl = _config["Keycloak:Url"] ?? "http://localhost:8080";
        var realm       = _config["Keycloak:Realm"] ?? "CityCare";
        var clientId    = _config["Keycloak:ClientId"] ?? "citycare-web";

        var logoutUrl = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/logout";

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id",     clientId),
            new KeyValuePair<string, string>("refresh_token", request.RefreshToken),
        });

        await _http.PostAsync(logoutUrl, body, cancellationToken);

        return NoContent();
    }
}

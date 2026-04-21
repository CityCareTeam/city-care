using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CityCare.Api.Services;

public class KeycloakService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakService> _logger;

    public KeycloakService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<KeycloakService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CreateUserAsync(string email, string username, string firstName , string lastName , string password)
    {
        try
        {
            // 1. Obtenir un token admin
            var adminToken = await GetAdminTokenAsync();

            // 2. Créer l'utilisateur
            var realm = _configuration["Keycloak:Realm"] ?? "citycare";
            var keycloakUrl = _configuration["Keycloak:Url"] ?? "http://localhost:8080";
            
            var createUserUrl = $"{keycloakUrl}/admin/realms/{realm}/users";

            var userPayload = new
            {
                username = username,
                email = email,
                enabled = true,
                emailVerified = false,
                firstName = firstName,
                lastName = lastName,
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = password,
                        temporary = false
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, createUserUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(userPayload),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erreur lors de la création de l'utilisateur Keycloak: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Erreur lors de la création de l'utilisateur: {response.StatusCode}");
            }

            // 3. Récupérer l'ID de l'utilisateur créé depuis le header Location
            var location = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(location))
            {
                throw new Exception("Impossible de récupérer l'ID de l'utilisateur créé");
            }

            var userId = location.Split('/').Last();

            // 4. Assigner le rôle "citizen" par défaut
            await AssignRoleToUserAsync(userId, "citizen", adminToken);

            _logger.LogInformation("Utilisateur créé dans Keycloak avec l'ID: {UserId}", userId);
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de l'utilisateur dans Keycloak");
            throw;
        }
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var keycloakUrl = _configuration["Keycloak:Url"] ?? "http://localhost:8080";
        // Utiliser le realm "master" pour l'authentification admin
        var adminRealm = "master";
        var clientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var adminUsername = _configuration["Keycloak:AdminUsername"] ?? "admin";
        var adminPassword = _configuration["Keycloak:AdminPassword"] ?? "admin";

        var tokenUrl = $"{keycloakUrl}/realms/{adminRealm}/protocol/openid-connect/token";

        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("username", adminUsername),
            new KeyValuePair<string, string>("password", adminPassword),
            new KeyValuePair<string, string>("grant_type", "password")
        });

        var response = await _httpClient.PostAsync(tokenUrl, tokenRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
        
        return tokenResponse.GetProperty("access_token").GetString() 
            ?? throw new Exception("Token d'accès non reçu");
    }

    private async Task AssignRoleToUserAsync(string userId, string roleName, string adminToken)
    {
        var keycloakUrl = _configuration["Keycloak:Url"] ?? "http://localhost:8080";
        var realm = _configuration["Keycloak:Realm"] ?? "citycare";

        // 1. Récupérer l'ID du rôle
        var rolesUrl = $"{keycloakUrl}/admin/realms/{realm}/roles/{roleName}";
        var roleRequest = new HttpRequestMessage(HttpMethod.Get, rolesUrl);
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var roleResponse = await _httpClient.SendAsync(roleRequest);
        roleResponse.EnsureSuccessStatusCode();

        var roleContent = await roleResponse.Content.ReadAsStringAsync();
        var role = JsonSerializer.Deserialize<JsonElement>(roleContent);

        // 2. Assigner le rôle à l'utilisateur
        var assignRoleUrl = $"{keycloakUrl}/admin/realms/{realm}/users/{userId}/role-mappings/realm";
        var assignRequest = new HttpRequestMessage(HttpMethod.Post, assignRoleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new[] { role }),
                Encoding.UTF8,
                "application/json")
        };
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var assignResponse = await _httpClient.SendAsync(assignRequest);
        assignResponse.EnsureSuccessStatusCode();

        _logger.LogInformation("Rôle {RoleName} assigné à l'utilisateur {UserId}", roleName, userId);
    }
}

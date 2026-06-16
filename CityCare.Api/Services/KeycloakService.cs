using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CityCare.Api.Dtos.Admin;

namespace CityCare.Api.Services;


public class KeycloakService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    

    public KeycloakService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<KeycloakService> logger,
        IHttpClientFactory httpClientFactory)
    
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> CreateUserAsync(string email, string username, string firstName, string lastName, string password)
    {
        var adminToken = await GetAdminTokenAsync();

        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];

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
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erreur création utilisateur Keycloak: {(int)response.StatusCode} - {content}");

        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(location))
            throw new Exception("Utilisateur créé, mais impossible de récupérer l'ID");

        var userId = location.Split('/').Last();

        _logger.LogInformation("Utilisateur créé dans Keycloak avec l'ID: {UserId}", userId);
        return userId;
    }

    public async Task UpdateUserAsync(string keycloakId, string? email, string? username, string? firstName, string? lastName)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var url = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}";

        var payload = new Dictionary<string, object>();
        if (email is not null) payload["email"] = email;
        if (username is not null) payload["username"] = username;
        if (firstName is not null) payload["firstName"] = firstName;
        if (lastName is not null) payload["lastName"] = lastName;

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erreur mise à jour utilisateur Keycloak: {(int)response.StatusCode} - {content}");
        }

        _logger.LogInformation("Utilisateur Keycloak {KeycloakId} mis à jour", keycloakId);
    }

    public async Task UpdatePasswordAsync(string keycloakId, string newPassword)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var url = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}/reset-password";

        var payload = new { type = "password", value = newPassword, temporary = false };
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erreur mise à jour mot de passe Keycloak: {(int)response.StatusCode} - {content}");
        }

        _logger.LogInformation("Mot de passe mis à jour pour l'utilisateur Keycloak {KeycloakId}", keycloakId);
    }

    public async Task AssignRealmRoleAsync(string keycloakId, string roleName)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var roleMappingsUrl = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}/role-mappings/realm";
        var appRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "citizen", "agent", "admin" };

        // Récupérer les rôles actuels
        var getCurrentReq = new HttpRequestMessage(HttpMethod.Get, roleMappingsUrl);
        getCurrentReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var getCurrentResp = await _httpClient.SendAsync(getCurrentReq);
        var currentRolesJson = await getCurrentResp.Content.ReadAsStringAsync();
        if (!getCurrentResp.IsSuccessStatusCode)
            throw new Exception($"Impossible de récupérer les rôles de l'utilisateur {keycloakId}: {(int)getCurrentResp.StatusCode} - {currentRolesJson}");

        // Identifier les rôles app à retirer
        using var currentDoc = JsonDocument.Parse(currentRolesJson);
        var rolesToRemove = currentDoc.RootElement.EnumerateArray()
            .Where(r => appRoleNames.Contains(r.GetProperty("name").GetString()!))
            .Select(r => new { id = r.GetProperty("id").GetString(), name = r.GetProperty("name").GetString() })
            .ToList();

        // Retirer les rôles app existants
        if (rolesToRemove.Count > 0)
        {
            var removeReq = new HttpRequestMessage(HttpMethod.Delete, roleMappingsUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(rolesToRemove), Encoding.UTF8, "application/json")
            };
            removeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            await _httpClient.SendAsync(removeReq);
        }

        // Récupérer la représentation du nouveau rôle
        var getRoleReq = new HttpRequestMessage(HttpMethod.Get, $"{keycloakUrl}/admin/realms/{realm}/roles/{roleName}");
        getRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var getRoleResp = await _httpClient.SendAsync(getRoleReq);

        if (!getRoleResp.IsSuccessStatusCode)
        {
            var errorBody = await getRoleResp.Content.ReadAsStringAsync();
            throw new Exception($"Rôle '{roleName}' introuvable dans Keycloak: {(int)getRoleResp.StatusCode} - {errorBody}");
        }

        using var roleDoc = JsonDocument.Parse(await getRoleResp.Content.ReadAsStringAsync());
        var roleId = roleDoc.RootElement.GetProperty("id").GetString();
        var fetchedRoleName = roleDoc.RootElement.GetProperty("name").GetString();

        // Assigner le nouveau rôle
        var assignPayload = new[] { new { id = roleId, name = fetchedRoleName } };
        var assignReq = new HttpRequestMessage(HttpMethod.Post, roleMappingsUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(assignPayload), Encoding.UTF8, "application/json")
        };
        assignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var assignResp = await _httpClient.SendAsync(assignReq);

        if (!assignResp.IsSuccessStatusCode)
        {
            var err = await assignResp.Content.ReadAsStringAsync();
            throw new Exception($"Erreur assignation rôle Keycloak: {(int)assignResp.StatusCode} - {err}");
        }

        _logger.LogInformation("Rôle '{Role}' assigné à l'utilisateur Keycloak {KeycloakId}", roleName, keycloakId);
    }

    public async Task<List<KeycloakUserDto>> GetUsersAsync(int first = 0, int max = 100, string? search = null)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];

        var url = $"{keycloakUrl}/admin/realms/{realm}/users?first={first}&max={max}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Erreur récupération utilisateurs Keycloak: {(int)response.StatusCode} - {content}");

        using var doc = JsonDocument.Parse(content);
        var result = new List<KeycloakUserDto>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var id = element.GetProperty("id").GetString()!;
            var username = element.TryGetProperty("username", out var u) ? u.GetString() : null;
            var email = element.TryGetProperty("email", out var em) ? em.GetString() : null;
            var firstName = element.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
            var lastName = element.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;
            var enabled = element.TryGetProperty("enabled", out var en) && en.GetBoolean();

            var roles = await GetUserRealmRolesAsync(id, adminToken);
            var appRole = roles.FirstOrDefault(r => new[] { "citizen", "agent", "admin" }.Contains(r.ToLowerInvariant()))
                           ?? "citizen";

            result.Add(new KeycloakUserDto(id, username, email, firstName, lastName, enabled, appRole.ToLowerInvariant()));
        }

        return result;
    }

    private async Task<List<string>> GetUserRealmRolesAsync(string keycloakId, string adminToken)
    {
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var url = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}/role-mappings/realm";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return new List<string>();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }

    public async Task SetUserEnabledAsync(string keycloakId, bool enabled)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var url = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}";

        var payload = new { enabled };
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erreur mise à jour statut utilisateur Keycloak: {(int)response.StatusCode} - {content}");
        }

        _logger.LogInformation("Utilisateur Keycloak {KeycloakId} {Status}", keycloakId, enabled ? "activé" : "désactivé");
    }

    public async Task DeleteUserAsync(string keycloakId)
    {
        var adminToken = await GetAdminTokenAsync();
        var realm = _configuration["Keycloak:Realm"];
        var keycloakUrl = _configuration["Keycloak:Url"];
        var url = $"{keycloakUrl}/admin/realms/{realm}/users/{keycloakId}";

        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erreur suppression utilisateur Keycloak: {(int)response.StatusCode} - {content}");
        }

        _logger.LogInformation("Utilisateur Keycloak {KeycloakId} supprimé", keycloakId);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var keycloakUrl = _configuration["Keycloak:Url"];
        var realm = _configuration["Keycloak:Realm"];
        var clientId = _configuration["Keycloak:ApiClientId"];
        var clientSecret = _configuration["Keycloak:ApiClientSecret"];

        var tokenUrl = $"{keycloakUrl}/realms/{realm}/protocol/openid-connect/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["grant_type"] = "client_credentials"
        };

        var client = _httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(tokenUrl, content);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Impossible d'obtenir le token admin : {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}

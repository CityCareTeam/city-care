using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

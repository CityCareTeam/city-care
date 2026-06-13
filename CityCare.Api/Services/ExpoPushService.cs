using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CityCare.Api.Services;

public sealed class ExpoPushService
{
    private readonly HttpClient _http;
    private readonly ILogger<ExpoPushService> _logger;

    private const string ExpoEndpoint = "https://exp.host/--/api/v2/push/send";

    public ExpoPushService(IHttpClientFactory factory, ILogger<ExpoPushService> logger)
    {
        _http   = factory.CreateClient();
        _logger = logger;
    }

    public Task SendAsync(
        string token,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
        => SendBatchAsync([token], title, body, data, cancellationToken);

    public async Task SendBatchAsync(
        IEnumerable<string> tokens,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        var validTokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        if (validTokens.Count == 0)
            return;

        _logger.LogInformation("Expo push — envoi à {Count} destinataire(s), titre=\"{Title}\"", validTokens.Count, title);

        var messages = validTokens.Select(t => new ExpoMessage
        {
            To    = t,
            Title = title,
            Body  = body,
            Data  = data
        }).ToList();

        try
        {
            // Expo accepte jusqu'à 100 messages par appel.
            foreach (var batch in messages.Chunk(100))
            {
                var response = await _http.PostAsJsonAsync(ExpoEndpoint, batch, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning(
                        "Expo push échoué — statut={Status}, tokens={Tokens}",
                        (int)response.StatusCode,
                        string.Join(", ", batch.Select(m => m.To)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expo push erreur réseau — titre=\"{Title}\", {Count} destinataire(s)", title, validTokens.Count);
        }
    }

    private sealed class ExpoMessage
    {
        [JsonPropertyName("to")]
        public string To { get; set; } = null!;

        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("body")]
        public string Body { get; set; } = null!;

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }
}

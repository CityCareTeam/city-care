using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CityCare.Api.Services;

public sealed class GeocodeService
{
    private readonly HttpClient _httpClient;

    public GeocodeService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // Nominatim exige un User-Agent (valeur "réelle", pas un UA vide).
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CityCarePlus", "1.0"));
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        if (!_httpClient.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR"));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr", 0.9));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
        }
    }

    public async Task<ReverseGeocodeResult?> ReverseGeocodeAsync(decimal lat, decimal lng, CancellationToken cancellationToken = default)
    {
        // Forcer le point décimal (.) quoi qu’il arrive.
        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lngStr = lng.ToString(CultureInfo.InvariantCulture);

        var url =
            $"https://nominatim.openstreetmap.org/reverse?lat={latStr}&lon={lngStr}&format=json&zoom=18&addressdetails=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Nominatim renvoie souvent une explication dans le body (texte ou json).
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            // On retourne null => le controller fera 404, mais au moins tu verras l’erreur en debug en mettant un breakpoint ici.
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("display_name", out var displayNameEl))
            return null;

        var addressLabel = displayNameEl.GetString();
        if (string.IsNullOrWhiteSpace(addressLabel))
            return null;

        string? city = null;
        string? postcode = null;
        string? countryCode = null;

        if (doc.RootElement.TryGetProperty("address", out var addressEl) && addressEl.ValueKind == JsonValueKind.Object)
        {
            city = TryGetString(addressEl, "city")
                ?? TryGetString(addressEl, "town")
                ?? TryGetString(addressEl, "village")
                ?? TryGetString(addressEl, "municipality");

            postcode = TryGetString(addressEl, "postcode");
            countryCode = TryGetString(addressEl, "country_code");
        }

        return new ReverseGeocodeResult(
            AddressLabel: addressLabel,
            City: city,
            Postcode: postcode,
            Country: string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.ToUpperInvariant());
    }

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var el) ? el.GetString() : null;
}

public sealed record ReverseGeocodeResult(
    string AddressLabel,
    string? City,
    string? Postcode,
    string? Country);
            
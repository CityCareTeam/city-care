using System.Net.Http.Headers;
using System.Text.Json;

namespace CityCare.Api.Services;

public sealed class GeocodeService
{
    private readonly HttpClient _httpClient;

    public GeocodeService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // Nominatim exige un User-Agent.
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CityCarePlus", "1.0"));
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<ReverseGeocodeResult?> ReverseGeocodeAsync(decimal lat, decimal lng, CancellationToken cancellationToken = default)
    {
        var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lng}&format=json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

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

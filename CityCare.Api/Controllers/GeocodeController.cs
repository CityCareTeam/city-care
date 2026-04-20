using CityCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityCare.Api.Controllers;

[ApiController]
[Route("geocode")]
public sealed class GeocodeController : ControllerBase
{
    private readonly GeocodeService _geocodeService;

    public GeocodeController(GeocodeService geocodeService)
    {
        _geocodeService = geocodeService;
    }

    [HttpGet("reverse")]
    [AllowAnonymous]
    public async Task<IActionResult> Reverse([FromQuery] decimal? lat, [FromQuery] decimal? lng, CancellationToken cancellationToken)
    {
        if (lat is null || lng is null)
            return BadRequest(new { error = "lat et lng sont requis." });

        var result = await _geocodeService.ReverseGeocodeAsync(lat.Value, lng.Value, cancellationToken);
        if (result is null)
            return NotFound();

        return Ok(new
        {
            address_label = result.AddressLabel,
            city = result.City,
            postcode = result.Postcode,
            country = result.Country ?? "FR"
        });
    }
}

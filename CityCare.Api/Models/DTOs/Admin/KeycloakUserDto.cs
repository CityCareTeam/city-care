namespace CityCare.Api.Dtos.Admin;

public sealed record KeycloakUserDto(
    string Id,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    string Role);

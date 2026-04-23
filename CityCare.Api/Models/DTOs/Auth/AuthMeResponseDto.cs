using CityCare.Core.Enums;

namespace CityCare.Api.Models.Dtos.Auth;

public sealed record AuthMeResponseDto(
    string? Sub, // UUID from Keycloak
    string? Email,
    string? Username,
    string? FirstName,
    string? LastName,
    List<string> Roles,
    UserRole? MainRole // Pour trouver quelle role est plus "fort" dans la list
);
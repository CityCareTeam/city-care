using CityCare.Core.Enums;

namespace CityCare.Api.Dtos.Auth;

public sealed record AuthMeResponseDTO(
    string? Sub, // UUID from Keycloak
    string? Email,
    string? Username,
    List<string> Roles,
    UserRole? MainRole // Pour trouver quelle role est plus "fort" dans la list
);
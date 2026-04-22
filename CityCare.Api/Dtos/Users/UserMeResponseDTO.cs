using CityCare.Core.Enums;

namespace CityCare.Api.Dtos.Users;

public sealed record UserMeResponseDTO(
    Guid Id,
    string KeycloakId,
    string Email,
    string? DisplayName,
    UserRole Role,
    DateTime CreatedAt,
    DateTime UpdatedAt);

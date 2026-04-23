namespace CityCare.Api.Dtos.Users;

public sealed record UserMeResponseDTO(
    Guid Id,
    string KeycloakId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

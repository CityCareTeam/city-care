namespace CityCare.Api.Dtos.Users;

public sealed record UserMeResponseDTO(
    Guid Id,
    string KeycloakId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

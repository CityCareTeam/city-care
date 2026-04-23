namespace CityCare.Api.Models.Dtos.Auth;

public sealed record LogoutRequestDto(
    string? RefreshToken
    );
namespace CityCare.Api.Models.Dtos.Auth;

public sealed record AuthLoginResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn
    );
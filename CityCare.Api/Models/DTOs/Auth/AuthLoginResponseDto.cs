namespace CityCare.Api.Dtos.Auth;

public sealed record AuthLoginResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn
    );
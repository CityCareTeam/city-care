namespace CityCare.Api.Dtos.Auth;

public sealed record AuthLoginRequestDto(
    string Username,
    string Password
    );
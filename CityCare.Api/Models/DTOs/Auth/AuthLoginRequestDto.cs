namespace CityCare.Api.Models.Dtos.Auth;

public sealed record AuthLoginRequestDto(
    string Username,
    string Password
    );
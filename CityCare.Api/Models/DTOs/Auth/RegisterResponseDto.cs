namespace CityCare.Api.Models.Dtos.Auth;

public sealed record RegisterResponseDto(
    string UserId,
    string Email,
    string Username,
    string LastName,
    string FirstName,
    string Message
);


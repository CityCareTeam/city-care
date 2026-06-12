using System.ComponentModel.DataAnnotations;

namespace CityCare.Api.Dtos.Users;

public sealed record UpdateMeRequestDto(
    [EmailAddress] string? Email,
    [MinLength(3)] string? Username,
    string? FirstName,
    string? LastName,
    [MinLength(8)] string? NewPassword
);

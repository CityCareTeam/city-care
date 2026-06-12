using System.ComponentModel.DataAnnotations;

namespace CityCare.Api.Dtos.Users;

public sealed record UpdateMeRequestDto(
    [EmailAddress] string? Email,
    [MinLength(3)] string? Username,
    [RegularExpression(@"^[\p{L}\s\-'.]+$", ErrorMessage = "Le prénom ne peut contenir que des lettres, espaces, tirets ou apostrophes.")]
    string? FirstName,
    [RegularExpression(@"^[\p{L}\s\-'.]+$", ErrorMessage = "Le nom ne peut contenir que des lettres, espaces, tirets ou apostrophes.")]
    string? LastName,
    [MinLength(8)] string? NewPassword
);

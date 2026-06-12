using System.ComponentModel.DataAnnotations;

namespace CityCare.Api.Models.Dtos.Auth;

public sealed record RegisterRequestDto(
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "L'email doit être valide")]
    [MaxLength(254, ErrorMessage = "L'email ne peut pas dépasser 254 caractères")]
    string Email,

    [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
    [MinLength(3, ErrorMessage = "Le nom d'utilisateur doit contenir au moins 3 caractères")]
    [MaxLength(30, ErrorMessage = "Le nom d'utilisateur ne peut pas dépasser 30 caractères")]
    string Username,

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [MaxLength(128, ErrorMessage = "Le mot de passe ne peut pas dépasser 128 caractères")]
    string Password,

    [Required(ErrorMessage = "Le nom est requis")]
    [MinLength(2, ErrorMessage = "Le nom doit contenir au moins 2 caractères")]
    [MaxLength(30, ErrorMessage = "Le nom ne peut pas dépasser 30 caractères")]
    [RegularExpression(@"^[\p{L}\s\-'.]+$", ErrorMessage = "Le nom ne peut contenir que des lettres, espaces, tirets ou apostrophes.")]
    string LastName,

    [Required(ErrorMessage = "Le prénom est requis")]
    [MinLength(2, ErrorMessage = "Le prénom doit contenir au moins 2 caractères")]
    [MaxLength(30, ErrorMessage = "Le prénom ne peut pas dépasser 30 caractères")]
    [RegularExpression(@"^[\p{L}\s\-'.]+$", ErrorMessage = "Le prénom ne peut contenir que des lettres, espaces, tirets ou apostrophes.")]
    string FirstName
);

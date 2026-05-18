using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace CityCare.Api.Models.Dtos.Auth;

public sealed record RegisterRequestDto(
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "L'email doit être valide")]
    string Email,
    
    [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
    [MinLength(3, ErrorMessage = "Le nom d'utilisateur doit contenir au moins 3 caractères")]
    string Username,
    
    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    string Password,
    
    [Required(ErrorMessage = "Le nom d'utilisateur est requis")] 
    [MaxLength(50, ErrorMessage = "Le nom d'utilisateur ne peut pas dépasser 50 caractères")]
    string LastName,
    
    [Required(ErrorMessage = "Le prénom est requis")]
    [MaxLength(50, ErrorMessage = "Le prénom ne peut pas dépasser 50 caractères")]
    string FirstName
    

);


using System.Security.Claims;
using CityCare.Core.Enums;

namespace CityCare.Api.Services;

public static class PrincipalClaimsHelper
{
    private static readonly HashSet<string> IgnoredRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "offline_access",
        "default-roles-citycare",
        "uma_authorization"
    };

    public static string? GetKeycloakSub(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
        principal.FindFirstValue("sub");

    public static string? GetEmail(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ??
        principal.FindFirstValue("email");

    public static string? GetDisplayName(ClaimsPrincipal principal) =>
        principal.FindFirstValue("preferred_username") ??
        principal.FindFirstValue("name");

    public static List<string> GetBusinessRoles(ClaimsPrincipal principal) =>
        principal.FindAll("roles")
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => !IgnoredRoles.Contains(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static UserRole? ResolveMainRole(ClaimsPrincipal principal)
    {
        var normalizedRoles = GetBusinessRoles(principal)
            .Select(r => r.Trim().ToLowerInvariant())
            .ToHashSet();

        if (normalizedRoles.Contains("admin"))
            return UserRole.Admin;
        if (normalizedRoles.Contains("agent"))
            return UserRole.Agent;
        if (normalizedRoles.Contains("citizen"))
            return UserRole.Citizen;

        return null;
    }
}
using System.Security.Claims;
using CityCare.Api.Dtos.Auth;
using CityCare.Core.Enums;

namespace CityCare.Api.Endpoints.Auth;




public static class AuthEndpoint {
    
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/me", GetMe)
            .RequireAuthorization();
    }

    public static IResult GetMe(ClaimsPrincipal user)
    {
        // Utiliser ClaimTypes pour les claims mappés automatiquement
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var username = user.FindFirst("preferred_username")?.Value;

        var ignoredRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "offline_access",
            "default-roles-citycare",
            "uma_authorization"
        };

        // Lire les rôles depuis le claim "roles" et ClaimTypes.Role
        var roles = user.FindAll("roles")
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => !ignoredRoles.Contains(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mainRole = ResolveMainRole(roles);

        var response = new AuthMeResponseDTO(
            Sub: sub,
            Email: email,
            Username: username,
            Roles: roles,
            MainRole: mainRole
        );

        return Results.Ok(response);
    }

    private static UserRole? ResolveMainRole(IEnumerable<string> roles)
    {
        var normalizedRoles = roles
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
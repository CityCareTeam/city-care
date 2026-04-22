using System.Security.Claims;
using CityCare.Core.Entities;
using CityCare.Core.Enums;
using CityCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Services;

/// <summary>
/// Résout l'utilisateur courant via le claim Keycloak <c>sub</c> (stocké en <see cref="User.KeycloakId"/>)
/// et crée la ligne en base au besoin (piste A : sync au premier appel).
/// </summary>
public sealed class CurrentUserService
{
    private readonly CityCareDbContext _db;

    public CurrentUserService(CityCareDbContext db)
    {
        _db = db;
    }

    public static string? GetKeycloakSub(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
        principal.FindFirstValue("sub");

    public async Task<User?> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var sub = GetKeycloakSub(principal);
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == sub, cancellationToken);
        if (existing is not null)
        {
            await ApplyClaimsToUserAsync(existing, principal, cancellationToken);
            return existing;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
            email = $"{sub}@users.local";

        var displayName = principal.FindFirstValue("preferred_username")
                          ?? principal.FindFirstValue("name");

        var role = ResolveMainRoleFromPrincipal(principal) ?? UserRole.Citizen;
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = sub,
            Email = email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            Role = role,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    private async Task ApplyClaimsToUserAsync(User user, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        var displayName = principal.FindFirstValue("preferred_username")
                            ?? principal.FindFirstValue("name");
        var role = ResolveMainRoleFromPrincipal(principal);

        var changed = false;
        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(user.Email, email.Trim(), StringComparison.Ordinal))
        {
            user.Email = email.Trim();
            changed = true;
        }

        if (displayName is not null)
        {
            var trimmed = displayName.Trim();
            if (!string.Equals(user.DisplayName, trimmed, StringComparison.Ordinal))
            {
                user.DisplayName = string.IsNullOrEmpty(trimmed) ? null : trimmed;
                changed = true;
            }
        }

        if (role is not null && user.Role != role)
        {
            user.Role = role.Value;
            changed = true;
        }

        if (!changed)
            return;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static UserRole? ResolveMainRoleFromPrincipal(ClaimsPrincipal principal)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "offline_access",
            "default-roles-citycare",
            "uma_authorization"
        };

        var roles = principal.FindAll("roles")
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Where(r => !ignored.Contains(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .ToHashSet();

        if (roles.Contains("admin"))
            return UserRole.Admin;
        if (roles.Contains("agent"))
            return UserRole.Agent;
        if (roles.Contains("citizen"))
            return UserRole.Citizen;

        return null;
    }
}

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

    public async Task<User?> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var sub = PrincipalClaimsHelper.GetKeycloakSub(principal);
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == sub, cancellationToken);
        if (existing is not null)
        {
            await ApplyClaimsToUserAsync(existing, principal, cancellationToken);
            return existing;
        }

        var email = PrincipalClaimsHelper.GetEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
            email = $"{sub}@users.local";

        var displayName = PrincipalClaimsHelper.GetDisplayName(principal);

        var role = PrincipalClaimsHelper.ResolveMainRole(principal) ?? UserRole.Citizen;
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
        var email = PrincipalClaimsHelper.GetEmail(principal);
        var displayName = PrincipalClaimsHelper.GetDisplayName(principal);
        var role = PrincipalClaimsHelper.ResolveMainRole(principal);

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
}

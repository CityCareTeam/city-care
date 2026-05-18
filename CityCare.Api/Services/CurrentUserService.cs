using System.Security.Claims;
using CityCare.Core.Entities;
using CityCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Services;

/// <summary>
/// Synchronise les utilisateurs Keycloak (<see cref="User.KeycloakId"/> = claim <c>sub</c>)
/// vers la base applicative, à l'inscription ou au premier appel authentifié.
/// </summary>
public sealed class CurrentUserService
{
    private readonly CityCareDbContext _db;

    public CurrentUserService(CityCareDbContext db)
    {
        _db = db;
    }

    public async Task<User> GetOrCreateByKeycloakIdAsync(string keycloakId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakId);

        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, cancellationToken);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = keycloakId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        return await GetOrCreateByKeycloakIdAsync(sub, cancellationToken);
    }
}

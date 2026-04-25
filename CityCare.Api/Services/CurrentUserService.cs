using System.Security.Claims;
using CityCare.Core.Entities;
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
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == sub, cancellationToken);
        if (existing is not null)
            return existing;
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = sub,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }
}

using CityCare.Core.Entities;
using CityCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Api.Services;

public sealed class NotificationService
{
    private readonly CityCareDbContext _db;

    public NotificationService(CityCareDbContext db) => _db = db;

    public async Task CreateAsync(
        Guid userId,
        string title,
        string body,
        string type,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default)
    {
        _db.Notifications.Add(new Notification
        {
            Id         = Guid.NewGuid(),
            UserId     = userId,
            Title      = title,
            Body       = body,
            Type       = type,
            IncidentId = incidentId,
            IsRead     = false,
            CreatedAt  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateBulkAsync(
        IEnumerable<Guid> userIds,
        string title,
        string body,
        string type,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            _db.Notifications.Add(new Notification
            {
                Id         = Guid.NewGuid(),
                UserId     = userId,
                Title      = title,
                Body       = body,
                Type       = type,
                IncidentId = incidentId,
                IsRead     = false,
                CreatedAt  = now
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Upsert pour les notifications "new_message" :
    /// si une notif non lue (userId, incidentId, "new_message") existe déjà,
    /// on incrémente MessageCount et on met à jour le titre/corps.
    /// Sinon on en crée une nouvelle avec MessageCount = 1.
    /// </summary>
    public async Task UpsertMessageNotifAsync(
        Guid userId,
        string addressLabel,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Notifications
            .Where(n => n.UserId     == userId
                     && n.IncidentId == incidentId
                     && n.Type       == "new_message"
                     && !n.IsRead)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (existing is not null)
        {
            var count = (existing.MessageCount ?? 1) + 1;
            existing.MessageCount = count;
            existing.Title        = $"{count} nouveaux messages";
            existing.Body         = $"Sur le signalement : {addressLabel}";
            existing.CreatedAt    = now; // remonte en tête de liste
        }
        else
        {
            _db.Notifications.Add(new Notification
            {
                Id           = Guid.NewGuid(),
                UserId       = userId,
                Title        = "Nouveau message",
                Body         = $"Sur le signalement : {addressLabel}",
                Type         = "new_message",
                IncidentId   = incidentId,
                IsRead       = false,
                MessageCount = 1,
                CreatedAt    = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// Upsert pour plusieurs destinataires (agents/admins).
    public async Task UpsertMessageNotifBulkAsync(
        IEnumerable<Guid> userIds,
        string addressLabel,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
            await UpsertMessageNotifAsync(userId, addressLabel, incidentId, cancellationToken);
    }
}

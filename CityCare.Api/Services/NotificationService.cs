using CityCare.Core.Entities;
using CityCare.Infrastructure.Persistence;

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
}

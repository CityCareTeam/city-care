using CityCare.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityCare.Infrastructure.Persistence;

public class CityCareDbContext : DbContext
{
    public CityCareDbContext(DbContextOptions<CityCareDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentStatusHistory> IncidentStatusHistories => Set<IncidentStatusHistory>();

    // ─── AJOUT Lots 2 & 3 ───────────────────────────────────────────
    public DbSet<IncidentMessage> IncidentMessages => Set<IncidentMessage>();
    public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
    // ────────────────────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.KeycloakId)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(u => u.KeycloakId).IsUnique();
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.Description)
                .IsRequired();

            entity.Property(i => i.AddressLabel)
                .IsRequired();

            entity.Property(i => i.Latitude)
                .HasPrecision(9, 6);

            entity.Property(i => i.Longitude)
                .HasPrecision(9, 6);

            entity.HasOne(i => i.AuthorUser)
                .WithMany(u => u.Incidents)
                .HasForeignKey(i => i.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.AuthorUserId);
            entity.HasIndex(i => new { i.Latitude, i.Longitude });
        });

        modelBuilder.Entity<IncidentStatusHistory>(entity =>
        {
            entity.ToTable("incident_status_history");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.Comment)
                .HasMaxLength(1000);

            entity.Property(h => h.ChangedAt)
                .IsRequired();

            entity.HasOne(h => h.Incident)
                .WithMany()
                .HasForeignKey(h => h.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => h.IncidentId);
            entity.HasIndex(h => h.ChangedByUserId);
            entity.HasIndex(h => h.ChangedAt);
        });

        // ─── AJOUT Lot 2 : messages d'incident ──────────────────────
        modelBuilder.Entity<IncidentMessage>(entity =>
        {
            entity.ToTable("incident_messages");

            entity.HasKey(m => m.Id);

            entity.Property(m => m.AuthorRole)
                .HasMaxLength(50);

            entity.Property(m => m.Content)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(m => m.CreatedAt)
                .IsRequired();

            entity.HasOne<Incident>()
                .WithMany()
                .HasForeignKey(m => m.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => m.IncidentId);
            entity.HasIndex(m => m.CreatedAt);
            entity.HasIndex(m => m.AuthorUserId);
        });

        // ─── AJOUT Lot 3 : préférences de notification ──────────────
        modelBuilder.Entity<UserNotificationSettings>(entity =>
        {
            entity.ToTable("user_notification_settings");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.EmailEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(s => s.PushEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(s => s.FollowedTypes)
                .IsRequired()
                .HasDefaultValue("");

            entity.Property(s => s.CreatedAt)
                .IsRequired();

            entity.Property(s => s.UpdatedAt)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.UserId).IsUnique();
        });
        // ────────────────────────────────────────────────────────────
    }
}
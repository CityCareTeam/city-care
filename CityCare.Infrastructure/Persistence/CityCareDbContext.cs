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
    public DbSet<IncidentPhoto> IncidentPhotos => Set<IncidentPhoto>();
    public DbSet<IncidentVote> IncidentVotes => Set<IncidentVote>();

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

        modelBuilder.Entity<IncidentPhoto>(entity =>
        {
            entity.ToTable("incident_photos");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.ObjectKey)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(p => p.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(p => p.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(p => p.CreatedAt)
                .IsRequired();

            entity.HasOne(p => p.Incident)
                .WithMany()
                .HasForeignKey(p => p.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.UploadedByUser)
                .WithMany()
                .HasForeignKey(p => p.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.IncidentId);
            entity.HasIndex(p => p.UploadedByUserId);
        });

        modelBuilder.Entity<IncidentVote>(entity =>
        {
            entity.ToTable("incident_votes");

            entity.HasKey(v => v.Id);

            entity.Property(v => v.CreatedAt)
                .IsRequired();

            entity.HasOne(v => v.Incident)
                .WithMany()
                .HasForeignKey(v => v.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Un seul vote par utilisateur et par incident
            entity.HasIndex(v => new { v.IncidentId, v.UserId }).IsUnique();
        });
    }
}
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

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(u => u.DisplayName)
                .HasMaxLength(100);

            entity.HasIndex(u => u.KeycloakId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
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
    }
}
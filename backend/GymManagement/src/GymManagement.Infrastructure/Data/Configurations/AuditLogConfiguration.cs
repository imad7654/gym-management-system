using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Details)
            .HasColumnType("text");

        builder.Property(a => a.ActorName)
            .HasMaxLength(200);

        // The screen reads newest first, and filters by what was touched.
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        // No foreign key to Users on purpose. The actor's name is copied onto the row, and
        // a trail that could be broken by removing a user would not be much of a trail.
    }
}

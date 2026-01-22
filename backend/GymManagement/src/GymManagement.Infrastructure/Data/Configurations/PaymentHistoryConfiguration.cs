using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class PaymentHistoryConfiguration : IEntityTypeConfiguration<PaymentHistory>
{
    public void Configure(EntityTypeBuilder<PaymentHistory> builder)
    {
        builder.ToTable("PaymentHistories");

        builder.HasKey(ph => ph.Id);

        builder.Property(ph => ph.Action)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ph => ph.OldAmount)
            .HasPrecision(10, 2);

        builder.Property(ph => ph.NewAmount)
            .HasPrecision(10, 2);

        builder.Property(ph => ph.OldStatus)
            .HasMaxLength(20);

        builder.Property(ph => ph.NewStatus)
            .HasMaxLength(20);

        builder.Property(ph => ph.ChangeDescription)
            .HasColumnType("text");

        builder.HasIndex(ph => ph.PaymentId);
        builder.HasIndex(ph => ph.ClientId);
        builder.HasIndex(ph => ph.ChangedAt);

        builder.HasOne(ph => ph.Payment)
            .WithMany(p => p.PaymentHistories)
            .HasForeignKey(ph => ph.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ph => ph.Client)
            .WithMany(c => c.PaymentHistories)
            .HasForeignKey(ph => ph.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

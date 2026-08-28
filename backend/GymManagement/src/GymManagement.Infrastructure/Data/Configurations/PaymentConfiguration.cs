using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(10, 2);

        // Wider than Amount because an LBP figure for the same package runs to millions.
        builder.Property(p => p.AmountReceived)
            .HasPrecision(14, 2);

        builder.Property(p => p.ExchangeRate)
            .HasPrecision(14, 2);

        builder.Property(p => p.Currency)
            .HasConversion<string>()
            .HasMaxLength(3);

        builder.Property(p => p.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.TransactionReference)
            .HasMaxLength(100);

        builder.Property(p => p.Notes)
            .HasColumnType("text");

        builder.HasIndex(p => p.ClientId);
        builder.HasIndex(p => p.PaymentDate);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.PeriodStartDate, p.PeriodEndDate });

        builder.HasOne(p => p.Client)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Package)
            .WithMany(pkg => pkg.Payments)
            .HasForeignKey(p => p.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // A reversal points back at the payment it cancels. Restrict, not Cascade: deleting
        // the row a reversal refers to would leave the books unexplainable, and money rows
        // are never deleted anyway.
        builder.HasOne(p => p.ReversesPayment)
            .WithMany()
            .HasForeignKey(p => p.ReversesPaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ReversesPaymentId);

        builder.HasMany(p => p.PaymentHistories)
            .WithOne(ph => ph.Payment)
            .HasForeignKey(ph => ph.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

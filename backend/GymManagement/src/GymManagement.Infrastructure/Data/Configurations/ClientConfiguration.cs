using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Email)
            .HasMaxLength(255);

        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");

        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(c => c.PhoneNumber);

        builder.Property(c => c.Address)
            .HasColumnType("text");

        builder.Property(c => c.EmergencyContact)
            .HasMaxLength(100);

        builder.Property(c => c.EmergencyPhone)
            .HasMaxLength(20);

        builder.Property(c => c.ProfileImageUrl)
            .HasMaxLength(500);

        builder.Property(c => c.Notes)
            .HasColumnType("text");

        builder.Property(c => c.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.Gender)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.HasIndex(c => c.PaymentStatus);
        builder.HasIndex(c => c.IsActive);

        // Every membership status question is now a question about these two columns, so
        // this is the index that matters. The old index on the stored status column went
        // with the column.
        builder.HasIndex(c => c.MembershipEndDate);
        builder.HasIndex(c => c.IsSuspended);

        builder.HasOne(c => c.CurrentPackage)
            .WithMany(p => p.Clients)
            .HasForeignKey(c => c.CurrentPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Payments)
            .WithOne(p => p.Client)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.PaymentHistories)
            .WithOne(ph => ph.Client)
            .HasForeignKey(ph => ph.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

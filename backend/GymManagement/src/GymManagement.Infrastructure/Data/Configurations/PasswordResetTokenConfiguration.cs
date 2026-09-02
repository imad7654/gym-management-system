using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(t => t.Id);

        // 64 hex characters of SHA-256. Fixed length, so char rather than a varchar that
        // would only ever be filled to the same width.
        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        // Every lookup is by hash - the token arrives, is hashed, and this finds it. Unique
        // because two rows sharing a hash would make that lookup ambiguous.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        builder.HasIndex(t => t.UserId);

        // Cascade: a removed account's outstanding reset links should go with it rather
        // than being left pointing at a user row that is no longer there.
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

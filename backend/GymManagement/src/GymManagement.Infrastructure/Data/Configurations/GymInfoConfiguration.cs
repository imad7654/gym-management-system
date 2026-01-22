using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class GymInfoConfiguration : IEntityTypeConfiguration<GymInfo>
{
    public void Configure(EntityTypeBuilder<GymInfo> builder)
    {
        builder.ToTable("GymInfos");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.GymName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.LogoUrl)
            .HasMaxLength(500);

        builder.Property(g => g.Description)
            .HasColumnType("text");

        builder.Property(g => g.Address)
            .HasColumnType("text");

        builder.Property(g => g.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(g => g.Email)
            .HasMaxLength(255);

        builder.Property(g => g.FacebookUrl)
            .HasMaxLength(500);

        builder.Property(g => g.InstagramUrl)
            .HasMaxLength(500);

        builder.Property(g => g.TwitterUrl)
            .HasMaxLength(500);

        builder.Property(g => g.OperatingHours)
            .HasColumnType("json");

        builder.Property(g => g.HeroTitle)
            .HasMaxLength(255);

        builder.Property(g => g.HeroSubtitle)
            .HasColumnType("text");

        builder.Property(g => g.HeroImageUrl)
            .HasMaxLength(500);

        builder.Property(g => g.AboutTitle)
            .HasMaxLength(255);

        builder.Property(g => g.AboutContent)
            .HasColumnType("text");

        builder.Property(g => g.MetaTitle)
            .HasMaxLength(255);

        builder.Property(g => g.MetaDescription)
            .HasColumnType("text");
    }
}

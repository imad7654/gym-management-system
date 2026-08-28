using GymManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymManagement.Infrastructure.Data.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");

        builder.HasKey(r => r.Id);

        // Wide enough for a rate in the tens of thousands with room to spare, matching the
        // precision Payment.ExchangeRate already uses so a stored rate and the rate it came
        // from can never round differently.
        builder.Property(r => r.Rate)
            .HasPrecision(14, 2);

        builder.Property(r => r.EffectiveDate)
            .HasColumnType("date");

        // One rate per day. Setting the rate twice in a morning corrects the day's rate
        // rather than adding a second row that later queries would have to choose between.
        builder.HasIndex(r => r.EffectiveDate)
            .IsUnique();
    }
}

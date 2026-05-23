using ERP.Domain.Platform.Observability.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Platform;

public sealed class LegacyUsageStatConfiguration : IEntityTypeConfiguration<LegacyUsageStat>
{
    public void Configure(EntityTypeBuilder<LegacyUsageStat> builder)
    {
        builder.ToTable("legacy_usage_stats");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Category, x.UsageKey }).IsUnique();
        builder.Property(x => x.UsageKey).HasMaxLength(LegacyUsageStat.UsageKeyMaxLen).IsRequired();
        builder.Property(x => x.Successor).HasMaxLength(LegacyUsageStat.SuccessorMaxLen);
        builder.Property(x => x.LastCallerIp).HasMaxLength(LegacyUsageStat.CallerIpMaxLen);
        builder.Property(x => x.LastDetail).HasMaxLength(LegacyUsageStat.DetailMaxLen);
    }
}

public sealed class LegacyUsageHitConfiguration : IEntityTypeConfiguration<LegacyUsageHit>
{
    public void Configure(EntityTypeBuilder<LegacyUsageHit> builder)
    {
        builder.ToTable("legacy_usage_hits");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.HitAtUtc);
        builder.Property(x => x.UsageKey).HasMaxLength(LegacyUsageStat.UsageKeyMaxLen).IsRequired();
        builder.Property(x => x.Successor).HasMaxLength(LegacyUsageStat.SuccessorMaxLen);
        builder.Property(x => x.CallerIp).HasMaxLength(LegacyUsageStat.CallerIpMaxLen);
        builder.Property(x => x.Detail).HasMaxLength(LegacyUsageStat.DetailMaxLen);
    }
}

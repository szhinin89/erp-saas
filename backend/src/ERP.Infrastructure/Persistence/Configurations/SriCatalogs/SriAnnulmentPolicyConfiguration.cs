using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public sealed class SriAnnulmentPolicyConfiguration : IEntityTypeConfiguration<SriAnnulmentPolicy>
{
    public static readonly Guid PolicyId = Guid.Parse("41000000-0000-0000-0000-000000000001");
    public void Configure(EntityTypeBuilder<SriAnnulmentPolicy> b)
    {
        b.ToTable("sri_annulment_policy", "global");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(new SriAnnulmentPolicy { Id = PolicyId, Code = "ELECTRONIC_DOCUMENT_ONLINE_ANNULMENT" });
    }
}

public sealed class SriAnnulmentPolicyVersionConfiguration : IEntityTypeConfiguration<SriAnnulmentPolicyVersion>
{
    public static readonly Guid LegacySourceId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    public static readonly Guid CurrentSourceId = Guid.Parse("40000000-0000-0000-0000-000000000005");
    public void Configure(EntityTypeBuilder<SriAnnulmentPolicyVersion> b)
    {
        b.ToTable("sri_annulment_policy_version", "global", t =>
        {
            t.HasCheckConstraint("ck_sri_annulment_policy_dates", "valid_from IS NULL OR valid_until IS NULL OR valid_from <= valid_until");
            t.HasCheckConstraint("ck_sri_annulment_policy_deadline", "COALESCE((day_of_following_month BETWEEN 1 AND 28 AND days_after_issue IS NULL) OR (day_of_following_month IS NULL AND days_after_issue > 0), FALSE)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PolicyId).HasColumnName("policy_id");
        b.Property(x => x.ValidFrom).HasColumnName("valid_from");
        b.Property(x => x.ValidUntil).HasColumnName("valid_until");
        b.Property(x => x.DayOfFollowingMonth).HasColumnName("day_of_following_month");
        b.Property(x => x.DaysAfterIssue).HasColumnName("days_after_issue");
        b.Property(x => x.ExtendToNextBusinessDay).HasColumnName("extend_to_next_business_day");
        b.Property(x => x.NormativeSourceId).HasColumnName("normative_source_id");
        b.HasOne<SriAnnulmentPolicy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SriNormativeSource>().WithMany().HasForeignKey(x => x.NormativeSourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PolicyId, x.ValidFrom }).IsUnique().AreNullsDistinct(false);
        b.HasData(
            new SriAnnulmentPolicyVersion
            {
                Id = Guid.Parse("42000000-0000-0000-0000-000000000001"),
                PolicyId = SriAnnulmentPolicyConfiguration.PolicyId,
                ValidFrom = new DateOnly(2016, 2, 22), ValidUntil = new DateOnly(2025, 7, 31),
                DaysAfterIssue = 90, NormativeSourceId = LegacySourceId,
            },
            new SriAnnulmentPolicyVersion
            {
                Id = Guid.Parse("42000000-0000-0000-0000-000000000002"),
                PolicyId = SriAnnulmentPolicyConfiguration.PolicyId,
                ValidFrom = new DateOnly(2025, 8, 1), DayOfFollowingMonth = 7,
                ExtendToNextBusinessDay = true, NormativeSourceId = CurrentSourceId,
            });
    }
}

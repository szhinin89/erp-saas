using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Configuration;

public sealed class OrgSettingConfiguration : IEntityTypeConfiguration<OrgSetting>
{
    public void Configure(EntityTypeBuilder<OrgSetting> builder)
    {
        builder.ToTable("org_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(e => e.Scope)
            .HasColumnName("scope")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<OrgScope>(v, ignoreCase: true)
            )
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ScopeId).HasColumnName("scope_id").IsRequired();

        builder
            .Property(e => e.Key)
            .HasColumnName("key")
            .HasMaxLength(OrgSetting.KeyMaxLength)
            .IsRequired();

        builder
            .Property(e => e.Value)
            .HasColumnName("value")
            .HasMaxLength(OrgSetting.ValueMaxLength)
            .IsRequired(false);

        builder
            .Property(e => e.DataType)
            .HasColumnName("data_type")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<SettingDataType>(v, ignoreCase: true)
            )
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.Scope,
                e.ScopeId,
                e.Key,
            })
            .IsUnique()
            .HasDatabaseName("uq_org_settings_scope_key");

        builder
            .HasIndex(e => new { e.TenantId, e.CompanyId })
            .HasDatabaseName("ix_org_settings_tenant_company");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.Scope,
                e.ScopeId,
            })
            .HasDatabaseName("ix_org_settings_scope_lookup");
    }
}

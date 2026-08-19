using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Configuration;

public sealed class ConfigurationChangeLogConfiguration : IEntityTypeConfiguration<ConfigurationChangeLog>
{
    private const int KeyMaxLength = 200;
    private const int EntityTypeMaxLength = 40;
    private const int FieldNameMaxLength = 60;
    private const int ValueMaxLength = 4000;
    private const int ReasonMaxLength = 500;

    public void Configure(EntityTypeBuilder<ConfigurationChangeLog> builder)
    {
        builder.ToTable("configuration_change_log");

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

        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(KeyMaxLength);

        builder
            .Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(EntityTypeMaxLength)
            .IsRequired();

        builder.Property(e => e.EntityId).HasColumnName("entity_id");

        builder
            .Property(e => e.FieldName)
            .HasColumnName("field_name")
            .HasMaxLength(FieldNameMaxLength)
            .IsRequired();

        builder.Property(e => e.OldValue).HasColumnName("old_value").HasMaxLength(ValueMaxLength);
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasMaxLength(ValueMaxLength);

        builder
            .Property(e => e.ValueType)
            .HasColumnName("value_type")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<ConfigurationChangeValueType>(v, ignoreCase: true)
            )
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(e => e.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired();

        builder
            .Property(e => e.Source)
            .HasColumnName("source")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<ConfigurationChangeSource>(v, ignoreCase: true)
            )
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(ReasonMaxLength);
        builder.Property(e => e.IsSensitive).HasColumnName("is_sensitive").IsRequired();

        // CONFIG-FOUNDATION-P2-01: append-only, sin UPDATE/DELETE esperados desde la aplicación —
        // no requiere concurrencia optimista.

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.ChangedAtUtc,
            })
            .HasDatabaseName("ix_config_change_log_tenant_company_changed_at")
            .IsDescending(false, false, true);

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.Key,
            })
            .HasDatabaseName("ix_config_change_log_tenant_company_key");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.EntityType,
                e.EntityId,
            })
            .HasDatabaseName("ix_config_change_log_tenant_company_entity");

        builder
            .HasIndex(e => new
            {
                e.TenantId,
                e.CompanyId,
                e.Scope,
                e.ScopeId,
            })
            .HasDatabaseName("ix_config_change_log_tenant_company_scope");
    }
}

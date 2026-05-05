using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ConfigGlobalConfiguration : IEntityTypeConfiguration<ConfigGlobal>
{
    public void Configure(EntityTypeBuilder<ConfigGlobal> builder)
    {
        builder.ToTable("config_global");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(ConfigGlobal.KeyMaxLength).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(ConfigGlobal.ValueMaxLength).IsRequired();
        builder.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(ConfigGlobal.DataTypeMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique().HasDatabaseName("ux_config_global_tenant_key");
    }
}

public sealed class ConfigModuleConfiguration : IEntityTypeConfiguration<ConfigModule>
{
    public void Configure(EntityTypeBuilder<ConfigModule> builder)
    {
        builder.ToTable("config_module");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").HasMaxLength(ConfigModule.ModuleMaxLength).IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(ConfigModule.KeyMaxLength).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(ConfigModule.ValueMaxLength).IsRequired();
        builder.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(ConfigModule.DataTypeMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.TenantId, x.Module, x.Key }).IsUnique().HasDatabaseName("ux_config_module_tenant_module_key");
    }
}

public sealed class ConfigFeatureConfiguration : IEntityTypeConfiguration<ConfigFeature>
{
    public void Configure(EntityTypeBuilder<ConfigFeature> builder)
    {
        builder.ToTable("config_feature");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Feature).HasColumnName("feature").HasMaxLength(ConfigFeature.FeatureMaxLength).IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(ConfigFeature.KeyMaxLength).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(ConfigFeature.ValueMaxLength).IsRequired();
        builder.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(ConfigFeature.DataTypeMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.TenantId, x.Feature, x.Key }).IsUnique().HasDatabaseName("ux_config_feature_tenant_feature_key");
    }
}


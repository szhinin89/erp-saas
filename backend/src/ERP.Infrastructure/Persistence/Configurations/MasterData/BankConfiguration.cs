using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("master_banks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder
            .Property(x => x.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(Bank.MaxCountryCodeLength)
            .IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(Bank.MaxCodeLength)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Bank.MaxNameLength)
            .IsRequired();
        builder
            .Property(x => x.ShortName)
            .HasColumnName("short_name")
            .HasMaxLength(Bank.MaxShortNameLength);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_master_banks_tenant");

        // BANK-CATALOG-01: único por país + código (no solo código) — el catálogo ya está
        // preparado para más de un país, aunque hoy solo se sembra Ecuador.
        builder
            .HasIndex(x => new { x.TenantId, x.CountryCode, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_master_banks_tenant_country_code");
    }
}

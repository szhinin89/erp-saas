using ERP.Domain.Modules.Caja.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

public sealed class CashMovementReasonConfiguration : IEntityTypeConfiguration<CashMovementReason>
{
    public void Configure(EntityTypeBuilder<CashMovementReason> builder)
    {
        builder.ToTable("cash_movement_reasons");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(CashMovementReason.CodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(CashMovementReason.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.MovementType)
            .HasColumnName("movement_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
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

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_cash_movement_reasons_tenant_company_code");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.MovementType, x.IsActive })
            .HasDatabaseName("ix_cash_movement_reasons_tenant_company_type_active");
    }
}

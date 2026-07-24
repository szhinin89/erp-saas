using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

public sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("cash_movements");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id").IsRequired();

        builder.Property(x => x.MovementType).HasColumnName("movement_type")
            .HasConversion<int>().IsRequired();

        builder.Property(x => x.Amount).HasColumnName("amount")
            .HasColumnType("numeric(18,2)").IsRequired();

        builder.Property(x => x.Description).HasColumnName("description")
            .HasMaxLength(CashMovement.DescriptionMaxLen).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();

        // ── Referencia externa (desacoplada) ────────────────────────
        builder.Property(x => x.ReferenceType).HasColumnName("reference_type")
            .HasConversion<int>().IsRequired()
            .HasDefaultValue(CashReferenceType.None);

        builder.Property(x => x.ReferenceId).HasColumnName("reference_id");

        builder.Property(x => x.ReferenceNumber).HasColumnName("reference_number")
            .HasMaxLength(CashMovement.ReferenceNumberMaxLen);

        // ── Indexes ─────────────────────────────────────────────────
        builder.HasIndex(x => new { x.TenantId, x.CashSessionId })
            .HasDatabaseName("ix_cash_movements_tenant_session");

        builder.HasIndex(x => new { x.TenantId, x.MovementType })
            .HasDatabaseName("ix_cash_movements_tenant_type");

        builder.HasIndex(x => new { x.TenantId, x.ReferenceType, x.ReferenceId })
            .HasDatabaseName("ix_cash_movements_tenant_ref")
            .HasFilter("reference_id IS NOT NULL");
    }
}

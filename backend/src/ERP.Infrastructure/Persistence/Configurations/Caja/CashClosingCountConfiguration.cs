using ERP.Domain.Modules.Caja.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

public sealed class CashClosingCountConfiguration : IEntityTypeConfiguration<CashClosingCount>
{
    public void Configure(EntityTypeBuilder<CashClosingCount> builder)
    {
        builder.ToTable("cash_closing_counts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id").IsRequired();

        builder
            .Property(x => x.DenominationValue)
            .HasColumnName("denomination_value")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .Property(x => x.DenominationLabel)
            .HasColumnName("denomination_label")
            .HasMaxLength(CashClosingCount.DenominationLabelMaxLen)
            .IsRequired();

        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder
            .Property(x => x.Total)
            .HasColumnName("total")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CashSessionId })
            .HasDatabaseName("ix_cash_closing_counts_tenant_session");
    }
}

using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(
            "payments",
            t => t.HasCheckConstraint("chk_payments_amount_positive", "amount > 0")
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.Direction)
            .HasColumnName("direction")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.PartnerId).HasColumnName("partner_id").IsRequired();

        // numeric(18,2) — montos (CLAUDE.md, Estándar de Precisión Numérica INMUTABLE).
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .Property(x => x.PaymentDate)
            .HasColumnName("payment_date")
            .HasColumnType("date")
            .IsRequired();
        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id");
        builder
            .Property(x => x.FinancialDestinationId)
            .HasColumnName("financial_destination_id");
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.AppliedAtUtc).HasColumnName("applied_at_utc");
        builder.Property(x => x.ReversedAtUtc).HasColumnName("reversed_at_utc");
        builder.Property(x => x.ReverseReason).HasColumnName("reverse_reason").HasMaxLength(500);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // FK sin navegación de dominio — mismo criterio que AccountingPeriodId en JournalEntry:
        // Payment no referencia la instancia de BusinessPartner/PaymentMethod, solo su Id.
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        // ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — mismo criterio: FK sin navegación de
        // dominio, Restrict (un destino financiero en uso por pagos históricos no puede borrarse).
        builder
            .HasOne<CompanyFinancialDestination>()
            .WithMany()
            .HasForeignKey(x => x.FinancialDestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Líneas de aplicación — Cascade porque una línea no tiene sentido de existir sin su pago
        // (mismo criterio que JournalEntry -> JournalEntryLine).
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_payments_tenant_company");

        builder
            .HasIndex(x => new { x.TenantId, x.PartnerId })
            .HasDatabaseName("ix_payments_tenant_partner");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.Status,
            })
            .HasDatabaseName("ix_payments_tenant_company_status");
    }
}

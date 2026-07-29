using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

public sealed class PaymentApplicationLineConfiguration
    : IEntityTypeConfiguration<PaymentApplicationLine>
{
    public void Configure(EntityTypeBuilder<PaymentApplicationLine> builder)
    {
        // Invariante de dominio "exactamente uno de ReceivableId/PayableId" (PaymentApplicationLine
        // .CreateForReceivable/.CreateForPayable) reforzado también a nivel de BD — defensa en
        // profundidad, mismo criterio que los demás CHECK de esta fase.
        builder.ToTable(
            "payment_application_lines",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_payment_application_line_applied_amount_positive",
                    "applied_amount > 0"
                );
                t.HasCheckConstraint(
                    "chk_payment_application_line_document_xor",
                    "(receivable_id IS NOT NULL AND payable_id IS NULL) OR (receivable_id IS NULL AND payable_id IS NOT NULL)"
                );
            }
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PaymentId).HasColumnName("payment_id").IsRequired();
        builder.Property(x => x.ReceivableId).HasColumnName("receivable_id");
        builder.Property(x => x.PayableId).HasColumnName("payable_id");

        // Sin FK real — InstallmentId puede apuntar a SalesReceivableInstallment.Id o a
        // PurchasePayableInstallment.Id según la dirección del pago propietario; una FK física
        // requeriría dos columnas separadas o una tabla polimórfica, fuera de alcance de esta
        // fase (el propio Domain lo documenta como referencia suelta opcional).
        builder.Property(x => x.InstallmentId).HasColumnName("installment_id");

        // numeric(18,2) — montos (CLAUDE.md, Estándar de Precisión Numérica INMUTABLE).
        builder
            .Property(x => x.AppliedAmount)
            .HasColumnName("applied_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder
            .HasOne<SalesReceivable>()
            .WithMany()
            .HasForeignKey(x => x.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<PurchasePayable>()
            .WithMany()
            .HasForeignKey(x => x.PayableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PaymentId).HasDatabaseName("ix_payment_application_lines_payment");

        builder
            .HasIndex(x => x.ReceivableId)
            .HasDatabaseName("ix_payment_application_lines_receivable");

        builder.HasIndex(x => x.PayableId).HasDatabaseName("ix_payment_application_lines_payable");
    }
}

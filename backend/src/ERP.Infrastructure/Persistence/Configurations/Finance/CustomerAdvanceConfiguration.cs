using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>Mapeo EF de <see cref="CustomerAdvance"/> — ADR-052 §4.</summary>
public sealed class CustomerAdvanceConfiguration : IEntityTypeConfiguration<CustomerAdvance>
{
    public void Configure(EntityTypeBuilder<CustomerAdvance> builder)
    {
        builder.ToTable(
            "customer_advances",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_customer_advances_original_amount_positive",
                    "\"original_amount\" > 0"
                );
                t.HasCheckConstraint(
                    "chk_customer_advances_available_amount_range",
                    "\"available_amount\" >= 0 AND \"available_amount\" <= \"original_amount\""
                );
                t.HasCheckConstraint(
                    "chk_customer_advances_destination_xor",
                    "(\"company_bank_account_id\" IS NULL) <> (\"cash_register_id\" IS NULL)"
                );
                // 1=Confirmed, 2=Cancelled (terminal: sin saldo disponible).
                t.HasCheckConstraint(
                    "chk_customer_advances_status",
                    "\"status\" = 1 OR (\"status\" = 2 AND \"available_amount\" = 0)"
                );
            }
        );

        builder.HasKey(x => x.Id);
        builder
            .HasAlternateKey(x => new
            {
                x.Id,
                x.TenantId,
                x.CompanyId,
            })
            .HasName("ak_customer_advances_id_scope");
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        Immutable(builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired());
        Immutable(builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired());
        Immutable(builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired());
        Immutable(builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired());
        Immutable(
            builder
                .Property(x => x.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(CustomerAdvance.CurrencyCodeLen)
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.OriginalAmount)
                .HasColumnName("original_amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired()
        );
        builder
            .Property(x => x.AvailableAmount)
            .HasColumnName("available_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        Immutable(
            builder
                .Property(x => x.AdvanceDate)
                .HasColumnName("advance_date")
                .HasColumnType("date")
                .IsRequired()
        );
        Immutable(
            builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired()
        );
        Immutable(
            builder.Property(x => x.CompanyBankAccountId).HasColumnName("company_bank_account_id")
        );
        Immutable(builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id"));
        Immutable(
            builder
                .Property(x => x.ExternalReference)
                .HasColumnName("external_reference")
                .HasMaxLength(CustomerAdvance.ExternalReferenceMaxLen)
        );
        Immutable(
            builder
                .Property(x => x.Note)
                .HasColumnName("note")
                .HasMaxLength(CustomerAdvance.NoteMaxLen)
        );
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(x => x.IsIntact);

        // ── Relationships ────────────────────────────────────────────
        // FK compuesta: un movimiento no puede pertenecer a un anticipo de otra empresa o tenant.
        builder
            .HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => new
            {
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.TenantId,
                x.CompanyId,
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_advance_movements_advance_scope");

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CompanyBankAccount>()
            .WithMany()
            .HasForeignKey(x => x.CompanyBankAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CashRegister>()
            .WithMany()
            .HasForeignKey(x => x.CashRegisterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.CustomerId,
            })
            .HasDatabaseName("ix_customer_advances_tenant_company_customer");
    }

    private static void Immutable<TProperty>(PropertyBuilder<TProperty> property) =>
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
}

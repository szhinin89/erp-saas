using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

/// <summary>
/// Mapeo EF de <see cref="PaymentMethodAccount"/> — SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01.
/// </summary>
public sealed class PaymentMethodAccountConfiguration : IEntityTypeConfiguration<PaymentMethodAccount>
{
    public void Configure(EntityTypeBuilder<PaymentMethodAccount> builder)
    {
        builder.ToTable("payment_method_accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
        builder
            .Property(x => x.AccountingAccountId)
            .HasColumnName("accounting_account_id")
            .IsRequired();

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

        // ── Relationships ────────────────────────────────────────────────
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.PaymentMethodId,
            })
            .IsUnique()
            .HasDatabaseName("uq_payment_method_accounts_tenant_company_method");
    }
}

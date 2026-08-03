using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// Mapeo EF de <see cref="CompanyFinancialDestination"/> — diseño P0-02 §6.4, Fase 2. El CHECK
/// combinado por <c>DestinationTypeCode</c> deliberadamente no exige <c>IsActive=true</c>
/// estructural (corrección explícita del diseño) — <c>IsActive</c> es condición de uso en
/// <c>RegisterRefund</c> (Fase 8), nunca parte de la integridad estructural del catálogo.
/// </summary>
public sealed class CompanyFinancialDestinationConfiguration
    : IEntityTypeConfiguration<CompanyFinancialDestination>
{
    public void Configure(EntityTypeBuilder<CompanyFinancialDestination> builder)
    {
        builder.ToTable(
            "company_financial_destinations",
            t =>
                t.HasCheckConstraint(
                    "chk_company_financial_destination_type_fields",
                    "(\"destination_type_code\" = 1 AND \"bank_institution_code\" IS NOT NULL "
                        + "AND \"bank_account_identifier_normalized\" IS NOT NULL AND \"cash_register_id\" IS NULL) "
                        + "OR (\"destination_type_code\" = 2 AND \"cash_register_id\" IS NOT NULL "
                        + "AND \"bank_institution_code\" IS NULL AND \"bank_account_identifier_normalized\" IS NULL)"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(CompanyFinancialDestination.CodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(CompanyFinancialDestination.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.DestinationTypeCode)
            .HasColumnName("destination_type_code")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.AccountingAccountId).HasColumnName("accounting_account_id").IsRequired();
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(CompanyFinancialDestination.CurrencyCodeMaxLen)
            .IsRequired();
        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id");
        builder
            .Property(x => x.BankInstitutionCode)
            .HasColumnName("bank_institution_code")
            .HasMaxLength(CompanyFinancialDestination.BankInstitutionCodeMaxLen);
        builder
            .Property(x => x.BankAccountIdentifierNormalized)
            .HasColumnName("bank_account_identifier_normalized")
            .HasMaxLength(CompanyFinancialDestination.BankAccountIdentifierMaxLen);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

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
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CashRegister>()
            .WithMany()
            .HasForeignKey(x => x.CashRegisterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_company_financial_destinations_tenant_company");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_company_financial_destinations_tenant_company_code");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BankInstitutionCode,
                x.BankAccountIdentifierNormalized,
            })
            .IsUnique()
            .HasFilter("\"destination_type_code\" = 1")
            .HasDatabaseName("uq_company_financial_destinations_bank_identity");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.CashRegisterId })
            .IsUnique()
            .HasFilter("\"destination_type_code\" = 2")
            .HasDatabaseName("uq_company_financial_destinations_cash_register");
    }
}

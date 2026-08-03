using ERP.Domain.Modules.Finance.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>Mapeo EF de <see cref="CompanyFinancialDestinationAudit"/> (ADR-022, Entity Audit) — diseño P0-02 §20.1.</summary>
public sealed class CompanyFinancialDestinationAuditConfiguration
    : IEntityTypeConfiguration<CompanyFinancialDestinationAudit>
{
    public void Configure(EntityTypeBuilder<CompanyFinancialDestinationAudit> builder)
    {
        builder.ConfigureAuditBase("company_financial_destination_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(CompanyFinancialDestination.CodeMaxLen);
        builder
            .Property(x => x.OldName)
            .HasColumnName("old_name")
            .HasMaxLength(CompanyFinancialDestination.NameMaxLen);
        builder
            .Property(x => x.NewName)
            .HasColumnName("new_name")
            .HasMaxLength(CompanyFinancialDestination.NameMaxLen);
        builder.Property(x => x.OldIsActive).HasColumnName("old_is_active");
        builder.Property(x => x.NewIsActive).HasColumnName("new_is_active");
        builder.Property(x => x.OldAccountingAccountId).HasColumnName("old_accounting_account_id");
        builder.Property(x => x.NewAccountingAccountId).HasColumnName("new_accounting_account_id");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAtUtc })
            .HasDatabaseName("ix_company_financial_destination_audit_company_occurred_at");
    }
}

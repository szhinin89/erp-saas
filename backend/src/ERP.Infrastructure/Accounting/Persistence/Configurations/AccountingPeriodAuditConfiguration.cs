using ERP.Domain.Modules.Accounting.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

/// <summary>Mapeo EF de <see cref="AccountingPeriodAudit"/> (ADR-022, Entity Audit) — ACCOUNTING-07.</summary>
public sealed class AccountingPeriodAuditConfiguration : IEntityTypeConfiguration<AccountingPeriodAudit>
{
    public void Configure(EntityTypeBuilder<AccountingPeriodAudit> builder)
    {
        builder.ConfigureAuditBase("accounting_period_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        builder.Property(x => x.PeriodNumber).HasColumnName("period_number").IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.FiscalYear,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_accounting_period_audit_company_year_occurred_at");
    }
}

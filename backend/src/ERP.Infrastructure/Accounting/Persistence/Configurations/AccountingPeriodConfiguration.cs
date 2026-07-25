using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("accounting_periods");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        builder.Property(x => x.PeriodNumber).HasColumnName("period_number").IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc");
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Único por (CompanyId, FiscalYear, PeriodNumber) — todas propiedades planas del owner,
        // sin el problema de owned type visto en Account.Code.
        builder.HasIndex(x => new { x.CompanyId, x.FiscalYear, x.PeriodNumber })
            .IsUnique()
            .HasDatabaseName("uq_accounting_periods_company_year_period");

        // NOTA — el invariante "no existen períodos solapados dentro de una Company" (ADR-026
        // §6.1, ver <remarks> de AccountingPeriod.cs) NO está cubierto por este índice único:
        // dos períodos con distinto (FiscalYear, PeriodNumber) pero rangos de fecha superpuestos
        // pasarían esta constraint sin problema. Esa validación es cross-aggregate (requiere
        // comparar StartDate/EndDate contra las demás filas de la Company) y queda
        // explícitamente para Application/Repository, no para una constraint de BD en esta fase.
    }
}

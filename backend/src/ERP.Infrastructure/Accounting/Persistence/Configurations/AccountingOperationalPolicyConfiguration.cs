using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — versiones de la política operativa de Accounting por Company. Una
/// sola versión por (Company, EffectiveFrom); el no solapamiento lo garantiza el dominio bajo el
/// lock exclusivo por Company.
/// </summary>
public sealed class AccountingOperationalPolicyConfiguration
    : IEntityTypeConfiguration<AccountingOperationalPolicy>
{
    public void Configure(EntityTypeBuilder<AccountingOperationalPolicy> builder)
    {
        builder.ToTable("accounting_operational_policies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("date")
            .IsRequired();
        builder
            .Property(x => x.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("date");
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(AccountingOperationalPolicy.ReasonMaxLength);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();

        builder
            .HasIndex(x => new { x.CompanyId, x.EffectiveFrom })
            .IsUnique()
            .HasDatabaseName("uq_accounting_operational_policies_company_from");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_accounting_operational_policies_scope");
    }
}

using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

/// <summary>TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4).</summary>
public sealed class CompanySpecialTaxResponsibilityConfiguration
    : IEntityTypeConfiguration<CompanySpecialTaxResponsibility>
{
    public void Configure(EntityTypeBuilder<CompanySpecialTaxResponsibility> builder)
    {
        builder.ToTable("company_special_tax_responsibilities");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.SriTaxCategoryCode)
            .HasColumnName("sri_tax_category_code")
            .HasMaxLength(CompanySpecialTaxResponsibility.SriTaxCategoryCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.IsResponsibleOnSales)
            .HasColumnName("is_responsible_on_sales")
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(x => new { x.CompanyId, x.SriTaxCategoryCode })
            .IsUnique()
            .HasDatabaseName("uq_company_special_tax_responsibility");
        builder
            .HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_company_special_tax_responsibilities_tenant");
    }
}

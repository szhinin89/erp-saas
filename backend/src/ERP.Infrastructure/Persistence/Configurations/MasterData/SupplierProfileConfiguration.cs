using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class SupplierProfileConfiguration : IEntityTypeConfiguration<SupplierProfile>
{
    public void Configure(EntityTypeBuilder<SupplierProfile> builder)
    {
        builder.ToTable("master_supplier_profiles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.DefaultTaxSupportCode)
               .HasColumnName("default_tax_support_code")
               .HasMaxLength(SupplierProfile.TaxSupportCodeMaxLen);
        builder.Property(x => x.DefaultRetentionVatCode)
               .HasColumnName("default_retention_vat_code")
               .HasMaxLength(SupplierProfile.RetentionVatCodeMaxLen);
        builder.Property(x => x.DefaultRetentionIncomeCode)
               .HasColumnName("default_retention_income_code")
               .HasMaxLength(SupplierProfile.RetentionIncomeCodeMaxLen);
        builder.Property(x => x.PaymentTerms)
               .HasColumnName("payment_terms")
               .HasMaxLength(SupplierProfile.PaymentTermsMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.BusinessPartnerId)
               .IsUnique()
               .HasDatabaseName("uq_msp_business_partner");

        builder.HasIndex(x => x.SubscriberId)
               .HasDatabaseName("ix_msp_subscriber");
    }
}

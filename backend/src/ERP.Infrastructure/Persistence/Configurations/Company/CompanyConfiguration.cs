using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.TaxIdentificationNumber)
            .HasColumnName("tax_identification_number")
            .HasMaxLength(32)
            .IsRequired();
        builder
            .Property(x => x.IsTemporaryTaxIdentification)
            .HasColumnName("is_temporary_tax_identification")
            .HasDefaultValue(false)
            .IsRequired();
        builder
            .Property(x => x.TaxIdentificationStatus)
            .HasColumnName("tax_identification_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.TradeName).HasColumnName("trade_name").HasMaxLength(200);

        builder
            .Property(x => x.CorporateEmail)
            .HasColumnName("corporate_email")
            .HasMaxLength(Company.CorporateEmailMaxLen);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder
            .Property(x => x.Website)
            .HasColumnName("website")
            .HasMaxLength(Company.WebsiteMaxLen);

        builder
            .Property(x => x.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(3)
            .HasDefaultValue("ECU");
        builder
            .Property(x => x.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(64)
            .HasDefaultValue("America/Guayaquil");
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasDefaultValue("USD");
        builder.Property(x => x.TaxRegimeCode).HasColumnName("tax_regime_code").HasMaxLength(5);
        builder
            .Property(x => x.IsAccountingReq)
            .HasColumnName("is_accounting_req")
            .HasDefaultValue(false);
        builder
            .Property(x => x.SpecialTaxpayerNo)
            .HasColumnName("special_taxpayer_no")
            .HasMaxLength(200);
        builder
            .Property(x => x.IsForeignTrade)
            .HasColumnName("is_foreign_trade")
            .HasDefaultValue(false);
        builder
            .Property(x => x.WithholdsRenta)
            .HasColumnName("withholds_renta")
            .HasDefaultValue(true);
        builder.Property(x => x.WithholdsVat).HasColumnName("withholds_iva").HasDefaultValue(true);
        builder.Property(x => x.ExtraLegend).HasColumnName("extra_legend").HasMaxLength(500);
        builder
            .Property(x => x.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(5)
            .HasDefaultValue("es");
        builder.Property(x => x.LegalRepName).HasColumnName("legal_rep_name").HasMaxLength(200);
        builder
            .Property(x => x.LegalRepPosition)
            .HasColumnName("legal_rep_position")
            .HasMaxLength(100);
        builder
            .Property(x => x.LegalRepIdNumber)
            .HasColumnName("legal_rep_id_number")
            .HasMaxLength(20);
        builder
            .Property(x => x.LegalRepEmail)
            .HasColumnName("legal_rep_email")
            .HasMaxLength(Company.CorporateEmailMaxLen);
        builder.Property(x => x.LegalRepPhone).HasColumnName("legal_rep_phone").HasMaxLength(20);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Conservado por compatibilidad de esquema/contrato de integración con Platform.
        // Las companies quedan operativas de inmediato al provisionarse (sin wizard de onboarding).
        builder
            .Property(x => x.OnboardingCompleted)
            .HasColumnName("onboarding_completed")
            .HasDefaultValue(true)
            .IsRequired();
        builder
            .Property(x => x.OperationalStatus)
            .HasColumnName("operational_status")
            .HasDefaultValue(ERP.Domain.Modules.Company.Enums.CompanyOperationalStatus.Operational)
            .IsRequired();

        builder
            .HasIndex(x => x.TaxIdentificationNumber)
            .IsUnique()
            .HasDatabaseName("uq_company_tax_identification_number");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_company_tenant_id");

        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_tenants_tenant_id");

        builder
            .HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(x => x.TaxRegime)
            .WithMany()
            .HasForeignKey(x => x.TaxRegimeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

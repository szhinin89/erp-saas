using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Subscribers.Entities;
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
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.Ruc).HasColumnName("ruc").HasMaxLength(32).IsRequired();
        builder.Property(x => x.IsProvisionalTaxId).HasColumnName("is_provisional_tax_id").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.TaxIdStatus)
            .HasColumnName("tax_id_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.TradeName).HasColumnName("trade_name").HasMaxLength(200);
        builder.Property(x => x.MainAddress).HasColumnName("main_address").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(120);
        builder.Property(x => x.Website).HasColumnName("website").HasMaxLength(200);
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(3).HasDefaultValue("ECU");
        builder.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(64).HasDefaultValue("America/Guayaquil");
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(x => x.TaxRegimeCode).HasColumnName("tax_regime_code").HasMaxLength(5);
        builder.Property(x => x.IsAccountingReq).HasColumnName("is_accounting_req").HasDefaultValue(false);
        builder.Property(x => x.SpecialTaxpayerNo).HasColumnName("special_taxpayer_no").HasMaxLength(200);
        builder.Property(x => x.IsForeignTrade).HasColumnName("is_foreign_trade").HasDefaultValue(false);
        builder.Property(x => x.WithholdsRenta).HasColumnName("withholds_renta").HasDefaultValue(true);
        builder.Property(x => x.WithholdsVat).HasColumnName("withholds_iva").HasDefaultValue(true);
        builder.Property(x => x.EnvironmentCode).HasColumnName("environment_code").HasDefaultValue((short)2);
        builder.Property(x => x.EmissionTypeCode).HasColumnName("emission_type_code").HasDefaultValue((short)1);
        builder.Property(x => x.WsdlRecvTest).HasColumnName("wsdl_recv_test").HasMaxLength(500);
        builder.Property(x => x.WsdlAuthTest).HasColumnName("wsdl_auth_test").HasMaxLength(500);
        builder.Property(x => x.WsdlRecvProd).HasColumnName("wsdl_recv_prod").HasMaxLength(500);
        builder.Property(x => x.WsdlAuthProd).HasColumnName("wsdl_auth_prod").HasMaxLength(500);
        builder.Property(x => x.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
        builder.Property(x => x.LogoBase64).HasColumnName("logo_base64").HasColumnType("text");
        builder.Property(x => x.BrandingJson).HasColumnName("branding_json").HasColumnType("jsonb");
        builder.Property(x => x.ExtraLegend).HasColumnName("extra_legend").HasMaxLength(500);
        builder.Property(x => x.ReceiptWidthMm).HasColumnName("receipt_width_mm").HasDefaultValue((short)80);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.Ruc).IsUnique().HasDatabaseName("uq_company_ruc");
        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_company_subscriber_id");

        builder.HasOne<Subscriber>()
            .WithMany()
            .HasForeignKey(x => x.SubscriberId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_subscribers_subscriber_id");

        builder.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TaxRegime).WithMany().HasForeignKey(x => x.TaxRegimeCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Environment).WithMany().HasForeignKey(x => x.EnvironmentCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmissionType).WithMany().HasForeignKey(x => x.EmissionTypeCode).OnDelete(DeleteBehavior.Restrict);
    }
}

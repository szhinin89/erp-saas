using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class BusinessPartnerConfiguration : IEntityTypeConfiguration<BusinessPartner>
{
    public void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        builder.ToTable("master_business_partners");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        // ── TaxIdentification (owned type — columnas aplanadas) ──────────
        builder.OwnsOne(x => x.Identification, id =>
        {
            id.Property(v => v.Type)
              .HasColumnName("identification_type")
              .HasMaxLength(TaxIdentification.TypeMaxLen)
              .IsRequired();
            id.Property(v => v.Number)
              .HasColumnName("identification_number")
              .HasMaxLength(TaxIdentification.NumberMaxLen)
              .IsRequired();
        });

        builder.Property(x => x.LegalName)
               .HasColumnName("legal_name")
               .HasMaxLength(BusinessPartner.LegalNameMaxLen)
               .IsRequired();
        builder.Property(x => x.TradeName)
               .HasColumnName("trade_name")
               .HasMaxLength(BusinessPartner.TradeNameMaxLen);
        builder.Property(x => x.LegalRepresentativeName)
               .HasColumnName("legal_representative_name")
               .HasMaxLength(BusinessPartner.LegalRepresentativeNameMaxLen);
        builder.Property(x => x.Email)
               .HasColumnName("email")
               .HasMaxLength(BusinessPartner.EmailMaxLen);
        builder.Property(x => x.Phone)
               .HasColumnName("phone")
               .HasMaxLength(BusinessPartner.PhoneMaxLen);
        builder.Property(x => x.CountryCode)
               .HasColumnName("country_code")
               .HasMaxLength(BusinessPartner.CountryCodeMaxLen)
               .IsFixedLength();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Navegación a perfiles (sin FK cascade para no propagar Disable) ──
        builder.HasOne(x => x.CustomerProfile)
               .WithOne(p => p.BusinessPartner)
               .HasForeignKey<CustomerProfile>(p => p.BusinessPartnerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupplierProfile)
               .WithOne(p => p.BusinessPartner)
               .HasForeignKey<SupplierProfile>(p => p.BusinessPartnerId)
               .OnDelete(DeleteBehavior.Restrict);

        // ── Índices ───────────────────────────────────────────────────────
        builder.HasIndex(x => new { x.SubscriberId })
               .HasDatabaseName("ix_mbp_subscriber");

        // NOTA: El índice único compuesto (subscriber_id, identification_type, identification_number)
        // se agrega manualmente en la migración AddMasterDataBC vía SQL raw:
        //   migrationBuilder.Sql("CREATE UNIQUE INDEX uq_mbp_subscriber_identification " +
        //     "ON master_business_partners (subscriber_id, identification_type, identification_number) " +
        //     "WHERE is_active = true;");
        // EF Core owned types no soportan HasIndex compuesto owner+owned por string en el builder del owner.
        // La unicidad se valida a nivel Application en ExistsByIdentificationAsync.
    }
}

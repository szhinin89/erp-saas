using ERP.Domain.Modules.Logistics.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Logistics;

public class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("carriers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.SubscriberId)
            .HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.IdentificationType)
            .HasColumnName("identification_type")
            .HasMaxLength(Carrier.MaxIdentificationTypeLength)
            .IsRequired();

        builder.Property(x => x.IdentificationNumber)
            .HasColumnName("identification_number")
            .HasMaxLength(Carrier.MaxIdentificationNumberLength)
            .IsRequired();

        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(Carrier.MaxLegalNameLength)
            .IsRequired();

        builder.Property(x => x.LicensePlate)
            .HasColumnName("license_plate")
            .HasMaxLength(Carrier.MaxLicensePlateLength)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(Carrier.MaxPhoneLength);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(Carrier.MaxEmailLength);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.IdentificationNumber })
            .IsUnique()
            .HasDatabaseName("ux_carriers_subscriber_identification");

        builder.HasIndex(x => x.SubscriberId)
            .HasDatabaseName("ix_carriers_subscriber_id");
    }
}

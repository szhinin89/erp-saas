using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class BusinessPartnerContactConfiguration : IEntityTypeConfiguration<BusinessPartnerContact>
{
    public void Configure(EntityTypeBuilder<BusinessPartnerContact> builder)
    {
        builder.ToTable("master_bp_contacts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.LocationId).HasColumnName("location_id");

        builder.Property(e => e.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(BusinessPartnerContact.FirstNameMaxLen)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(BusinessPartnerContact.LastNameMaxLen);

        builder.Property(e => e.Position)
            .HasColumnName("position")
            .HasMaxLength(BusinessPartnerContact.PositionMaxLen);

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasMaxLength(BusinessPartnerContact.PhoneMaxLen);

        builder.Property(e => e.Mobile)
            .HasColumnName("mobile")
            .HasMaxLength(BusinessPartnerContact.MobileMaxLen);

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(BusinessPartnerContact.EmailMaxLen);

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(BusinessPartnerContact.NotesMaxLen);

        builder.Property(e => e.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Ignore(e => e.FullName);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        // ── Índices ───────────────────────────────────────────────────────────
        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId, e.BusinessPartnerId })
            .HasDatabaseName("ix_mbpc_subscriber_company_bp");

        builder.HasIndex(e => e.LocationId)
            .HasDatabaseName("ix_mbpc_location");

        // FK opcional a BusinessPartnerLocation (null = contacto general)
        builder.HasOne<BusinessPartnerLocation>()
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

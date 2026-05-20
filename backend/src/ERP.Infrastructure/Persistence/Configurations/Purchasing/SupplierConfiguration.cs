using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(p => p.PersonType).HasColumnName("person_type").HasMaxLength(10).IsRequired();
        builder.Property(p => p.LegalName).HasColumnName("legal_name").HasMaxLength(Supplier.LegalNameMaxLen).IsRequired();
        builder.Property(p => p.Ruc).HasColumnName("ruc").HasMaxLength(Supplier.RucMaxLen).IsRequired();
        builder.Property(p => p.Email).HasColumnName("email").HasMaxLength(Supplier.EmailMaxLen);
        builder.Property(p => p.Phone).HasColumnName("phone").HasMaxLength(Supplier.PhoneMaxLen);
        builder.Property(p => p.Address).HasColumnName("address").HasMaxLength(Supplier.AddressMaxLen);
        builder.Property(p => p.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(Supplier.PaymentTermsMaxLen).IsRequired();
        builder.Property(p => p.CountryCode).HasColumnName("country_code").HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.TaxSupportCode).HasColumnName("tax_support_code").HasMaxLength(5);
        builder.Property(p => p.PaymentDays).HasColumnName("payment_days").HasDefaultValue((short)0);
        builder.Property(p => p.CreditLimit).HasColumnName("credit_limit").HasPrecision(18, 2);
        builder.Ignore(p => p.SriIdTypeCode);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(p => new { p.SubscriberId, p.Ruc }).IsUnique().HasDatabaseName("uq_supplier_ruc");
        builder.HasIndex(p => p.SubscriberId).HasDatabaseName("ix_supplier_subscriber_id");
    }
}

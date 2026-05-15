using ERP.Domain.Modules.Ventas.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(x => x.IdentificationType)
            .HasColumnName("identification_type")
            .HasMaxLength(Customer.IdentificationTypeMaxLen)
            .IsRequired();
        builder.Property(x => x.IdentificationNumber)
            .HasColumnName("identification_number")
            .HasMaxLength(Customer.IdentificationNumberMaxLen)
            .IsRequired();
        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(Customer.LegalNameMaxLen)
            .IsRequired();
        builder.Property(x => x.TradeName)
            .HasColumnName("trade_name")
            .HasMaxLength(Customer.TradeNameMaxLen);
        builder.Property(x => x.AddressLine)
            .HasColumnName("address_line")
            .HasMaxLength(Customer.AddressLineMaxLen);
        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(Customer.PhoneMaxLen);
        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(Customer.EmailMaxLen);
        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(Customer.NotesMaxLen);

        // ── Campos SRI / comerciales ─────────────────────────────
        builder.Property(x => x.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(3)
            .IsFixedLength();
        builder.Property(x => x.PaymentDays)
            .HasColumnName("payment_days")
            .HasDefaultValue((short)0);
        builder.Property(x => x.CreditLimit)
            .HasColumnName("credit_limit")
            .HasPrecision(18, 2);

        builder.Ignore(x => x.SriIdTypeCode);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.IdentificationType, x.IdentificationNumber })
            .IsUnique()
            .HasDatabaseName("ux_customers_tenant_doc");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_customers_tenant_id");
    }
}

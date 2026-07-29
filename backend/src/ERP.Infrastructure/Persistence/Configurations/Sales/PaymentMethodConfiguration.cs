using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(PaymentMethod.MaxCodeLength)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(PaymentMethod.MaxNameLength)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.RequiresReference).HasColumnName("requires_reference").IsRequired();
        builder.Property(x => x.IsCreditAllowed).HasColumnName("is_credit_allowed").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder
            .Property(x => x.DetailType)
            .HasColumnName("detail_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PaymentMethodDetailType.None);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_payment_methods_tenant_code");

        builder
            .HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("ix_payment_methods_tenant_active");
    }
}

using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> builder)
    {
        builder.ToTable("master_payment_terms");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(PaymentTerm.MaxCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(PaymentTerm.MaxNameLength).IsRequired();
        builder.Property(x => x.Installments).HasColumnName("installments").IsRequired();
        builder.Property(x => x.DaysBetweenInstallments).HasColumnName("days_between_installments").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(x => x.TotalDays);
        builder.Ignore(x => x.Summary);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_payment_terms_tenant");

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_payment_terms_tenant_code");
    }
}

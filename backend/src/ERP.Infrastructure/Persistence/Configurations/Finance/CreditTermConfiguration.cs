using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

public sealed class CreditTermConfiguration : IEntityTypeConfiguration<CreditTerm>
{
    public void Configure(EntityTypeBuilder<CreditTerm> builder)
    {
        builder.ToTable("credit_terms");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(CreditTerm.MaxCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(CreditTerm.MaxNameLength).IsRequired();
        builder.Property(x => x.Mode).HasColumnName("mode")
            .HasConversion<int>().IsRequired();
        builder.Property(x => x.TotalDays).HasColumnName("total_days").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Installments)
            .WithOne()
            .HasForeignKey(x => x.CreditTermId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_credit_terms_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_credit_terms_tenant_company_code");
    }
}

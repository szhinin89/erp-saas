using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Accounting.Enums;
using ERP.Domain.Accounting.ValueObjects;

namespace ERP.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(a => a.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                code => code.Value,
                value => new AccountCode(value));

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Nature)
            .HasColumnName("nature")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(a => a.ParentId)
            .HasColumnName("parent_id");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(a => a.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(a => new { a.TenantId, a.Code })
            .IsUnique()
            .HasDatabaseName("ix_accounts_tenant_code");
    }
}

using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // ── AccountCode VO — HasConversion a columna escalar (no OwnsOne). AccountCode tiene
        // una única propiedad (Value) sin comportamiento adicional; a diferencia de ItemCode
        // (3 propiedades, sí justifica OwnsOne), un VO de una sola propiedad se mapea mejor
        // como conversión escalar: evita el table-splitting de owned type y permite declarar
        // el índice único compuesto (CompanyId, Code) vía Fluent API estándar, sin la
        // limitación de EF Core que impide combinar una propiedad de owned type con una
        // propiedad del owner en el mismo índice (ver ItemConfiguration.cs, Code.SKU).
        builder
            .Property(x => x.Code)
            .HasConversion(c => c.Value, v => AccountCode.Create(v))
            .HasColumnName("code")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.ParentAccountId).HasColumnName("parent_account_id");
        builder
            .Property(x => x.AccountType)
            .HasColumnName("account_type")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.Nature).HasColumnName("nature").HasConversion<int>().IsRequired();
        builder.Property(x => x.AllowsPosting).HasColumnName("allows_posting").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // No FK a sí misma (ParentAccountId) todavía — jerarquía/detección de ciclos diferida
        // a Application/Repository (ver <remarks> de Account.cs). Columna plana sin constraint.

        builder
            .HasIndex(x => new { x.CompanyId, x.ParentAccountId })
            .HasDatabaseName("ix_accounts_company_parent");

        builder
            .HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_accounts_company_code");
    }
}

using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class EstablishmentConfiguration : IEntityTypeConfiguration<Establishment>
{
    public void Configure(EntityTypeBuilder<Establishment> builder)
    {
        builder.ToTable("establishment");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");

        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(Establishment.CodeMaxLen)
            .IsFixedLength()
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Establishment.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Address)
            .HasColumnName("address")
            .HasMaxLength(Establishment.AddressMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(Establishment.PhoneMaxLen);
        builder.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(x => x.IsSystemSeeded)
            .HasColumnName("is_system_seeded")
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_establishment_tenant_id");
        builder
            .HasIndex(x => new { x.TenantId, x.BranchId })
            .HasDatabaseName("ix_establishment_tenant_branch")
            .HasFilter("branch_id IS NOT NULL");
        builder
            .HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_estab_code");

        builder
            .HasOne(x => x.Company)
            .WithMany(x => x.Establishments)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

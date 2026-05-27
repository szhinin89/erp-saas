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
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(Establishment.CodeMaxLen).IsFixedLength().IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(Establishment.NameMaxLen).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address")
            .HasMaxLength(Establishment.AddressMaxLen).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone")
            .HasMaxLength(Establishment.PhoneMaxLen);
        builder.Property(x => x.IsMain).HasColumnName("is_main").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_establishment_subscriber_id");
        builder.HasIndex(x => x.BranchId).HasDatabaseName("ix_establishment_branch_id");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("uq_estab_code");

        builder.HasOne(x => x.Company)
            .WithMany(x => x.Establishments)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

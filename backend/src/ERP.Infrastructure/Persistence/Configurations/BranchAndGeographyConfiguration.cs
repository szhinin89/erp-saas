using ERP.Domain.Branches.Entities;
using ERP.Domain.Geography.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class GeoProvinceConfiguration : IEntityTypeConfiguration<GeoProvince>
{
    public void Configure(EntityTypeBuilder<GeoProvince> builder)
    {
        builder.ToTable("geo_provinces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(10);
        builder.Property(x => x.CountryId).HasColumnName("country_id").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.CountryId).HasDatabaseName("ix_geo_provinces_country_id");

        builder.HasOne<SriCountry>().WithMany().HasForeignKey(x => x.CountryId)
            .HasPrincipalKey(c => c.Iso2).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GeoCantonConfiguration : IEntityTypeConfiguration<GeoCanton>
{
    public void Configure(EntityTypeBuilder<GeoCanton> builder)
    {
        builder.ToTable("geo_cantons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(10);
        builder.Property(x => x.ProvinceId).HasColumnName("province_id").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.ProvinceId).HasDatabaseName("ix_geo_cantons_province_id");

        builder.HasOne<GeoProvince>().WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GeoParishConfiguration : IEntityTypeConfiguration<GeoParish>
{
    public void Configure(EntityTypeBuilder<GeoParish> builder)
    {
        builder.ToTable("geo_parishes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(10);
        builder.Property(x => x.CantonId).HasColumnName("canton_id").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.CantonId).HasDatabaseName("ix_geo_parishes_canton_id");

        builder.HasOne<GeoCanton>().WithMany().HasForeignKey(x => x.CantonId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(x => x.BranchType).HasColumnName("branch_type").HasMaxLength(50);
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.Phones).HasColumnName("phones").HasMaxLength(200);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(x => x.ManagerName).HasColumnName("manager_name").HasMaxLength(100);

        builder.Property(x => x.CountryId).HasColumnName("country_id").HasMaxLength(10);
        builder.Property(x => x.ProvinceId).HasColumnName("province_id").HasMaxLength(10);
        builder.Property(x => x.CantonId).HasColumnName("canton_id").HasMaxLength(10);
        builder.Property(x => x.ParishId).HasColumnName("parish_id").HasMaxLength(10);

        builder.Property(x => x.Latitude).HasColumnName("latitude").HasMaxLength(25);
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasMaxLength(25);
        builder.Property(x => x.StorageCapacity).HasColumnName("storage_capacity").HasColumnType("decimal(12,2)");
        builder.Property(x => x.DailySalesGoal).HasColumnName("daily_sales_goal").HasColumnType("decimal(12,2)");
        builder.Property(x => x.RechargeOption).HasColumnName("recharge_option").HasMaxLength(20);

        builder.Property(x => x.IsMainBranch).HasColumnName("is_main_branch").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_branches_subscriber_id");

        builder.HasOne<SriCountry>().WithMany().HasForeignKey(x => x.CountryId)
            .HasPrincipalKey(c => c.Iso2).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoProvince>().WithMany().HasForeignKey(x => x.ProvinceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoCanton>().WithMany().HasForeignKey(x => x.CantonId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoParish>().WithMany().HasForeignKey(x => x.ParishId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

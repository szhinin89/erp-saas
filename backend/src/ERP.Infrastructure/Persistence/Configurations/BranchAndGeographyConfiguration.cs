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
        builder.ToTable("geo_provinces", schema: "global");
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
        builder.ToTable("geo_cantons", schema: "global");
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
        builder.ToTable("geo_parishes", schema: "global");
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
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(20);

        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(200);
        builder.Property(x => x.SecondaryPhone).HasColumnName("secondary_phone").HasMaxLength(200);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(x => x.Website).HasColumnName("website").HasMaxLength(200);

        builder.Property(x => x.ManagerName).HasColumnName("manager_name").HasMaxLength(100);
        builder.Property(x => x.ManagerPosition).HasColumnName("manager_position").HasMaxLength(100);
        builder.Property(x => x.ManagerEmail).HasColumnName("manager_email").HasMaxLength(150);
        builder.Property(x => x.ManagerPhone).HasColumnName("manager_phone").HasMaxLength(200);

        builder.Property(x => x.CountryId).HasColumnName("country_id").HasMaxLength(10);
        builder.Property(x => x.ProvinceId).HasColumnName("province_id").HasMaxLength(10);
        builder.Property(x => x.CantonId).HasColumnName("canton_id").HasMaxLength(10);
        builder.Property(x => x.ParishId).HasColumnName("parish_id").HasMaxLength(10);

        builder.Property(x => x.Latitude).HasColumnName("latitude").HasMaxLength(25);
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasMaxLength(25);

        builder.Property(x => x.OpeningDate).HasColumnName("opening_date").HasColumnType("date");
        builder.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasMaxLength(1000);

        builder.Property(x => x.IsMainBranch).HasColumnName("is_main_branch").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_branches_tenant_id");

        builder.HasOne<SriCountry>().WithMany().HasForeignKey(x => x.CountryId)
            .HasPrincipalKey(c => c.Iso2).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoProvince>().WithMany().HasForeignKey(x => x.ProvinceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoCanton>().WithMany().HasForeignKey(x => x.CantonId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoParish>().WithMany().HasForeignKey(x => x.ParishId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

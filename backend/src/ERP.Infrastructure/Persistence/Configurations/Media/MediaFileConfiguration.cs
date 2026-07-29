using ERP.Domain.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Media;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("media_files");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(MediaFile.MaxFileNameLength)
            .IsRequired();
        builder
            .Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(MediaFile.MaxFileNameLength)
            .IsRequired();
        builder
            .Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(MediaFile.MaxContentTypeLength)
            .IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();

        builder
            .Property(x => x.StorageProvider)
            .HasColumnName("storage_provider")
            .HasMaxLength(MediaFile.MaxStorageProviderLength)
            .IsRequired();
        builder
            .Property(x => x.StoragePath)
            .HasColumnName("storage_path")
            .HasMaxLength(MediaFile.MaxStoragePathLength)
            .IsRequired();
        builder
            .Property(x => x.PublicUrl)
            .HasColumnName("public_url")
            .HasMaxLength(MediaFile.MaxPublicUrlLength);

        builder
            .Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder
            .Property(x => x.OwnerType)
            .HasColumnName("owner_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(MediaFile.MaxRoleLength);

        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired();

        builder.Property(x => x.Width).HasColumnName("width");
        builder.Property(x => x.Height).HasColumnName("height");
        builder
            .Property(x => x.Checksum)
            .HasColumnName("checksum")
            .HasMaxLength(MediaFile.MaxChecksumLength);
        builder
            .Property(x => x.AltText)
            .HasColumnName("alt_text")
            .HasMaxLength(MediaFile.MaxAltTextLength);

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

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_media_files_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.OwnerType,
                x.OwnerId,
                x.Role,
                x.IsPrimary,
                x.IsActive,
            })
            .HasDatabaseName("ix_media_files_owner_role_primary");
    }
}

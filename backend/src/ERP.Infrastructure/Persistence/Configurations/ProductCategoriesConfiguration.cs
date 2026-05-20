using ERP.Domain.Products.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ProductLineConfiguration : IEntityTypeConfiguration<ProductLine>
{
    public void Configure(EntityTypeBuilder<ProductLine> builder)
    {
        builder.ToTable("product_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_product_lines_subscriber_code");
    }
}

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.LineId).HasColumnName("line_id").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.LineId })
            .HasDatabaseName("ix_product_categories_subscriber_line");
        builder.HasIndex(x => new { x.SubscriberId, x.LineId, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_product_categories_subscriber_line_code");
    }
}

public class ProductSubcategoryConfiguration : IEntityTypeConfiguration<ProductSubcategory>
{
    public void Configure(EntityTypeBuilder<ProductSubcategory> builder)
    {
        builder.ToTable("product_subcategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.SubscriberId, x.CategoryId })
            .HasDatabaseName("ix_product_subcategories_subscriber_category");
        builder.HasIndex(x => new { x.SubscriberId, x.CategoryId, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_product_subcategories_subscriber_category_code");
    }
}


using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Items;

public sealed class ItemCategoryNodeConfiguration : IEntityTypeConfiguration<ItemCategoryNode>
{
    public void Configure(EntityTypeBuilder<ItemCategoryNode> builder)
    {
        builder.ToTable("item_category_nodes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Path).HasMaxLength(500).IsRequired().HasDefaultValue("/");
        builder.Property(e => e.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.SortOrder).HasDefaultValue(0);
        builder.Property(e => e.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);

        builder.HasOne<ItemCategoryNode>()
            .WithMany()
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ParentId });
        builder.HasIndex(e => new { e.TenantId, e.Path });
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
    }
}

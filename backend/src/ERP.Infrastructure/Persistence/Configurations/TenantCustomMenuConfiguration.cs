using ERP.Domain.Navigation.Entities;
using ERP.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class TenantCustomMenuConfiguration : IEntityTypeConfiguration<TenantCustomMenu>
{
    public void Configure(EntityTypeBuilder<TenantCustomMenu> builder)
    {
        builder.ToTable("tenant_custom_menus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.MenuConfigJson)
            .HasColumnName("menu_config")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder
            .HasIndex(x => x.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_tenant_custom_menus_tenant");
        builder
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_custom_menus_tenant_id");
    }
}

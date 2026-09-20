using ERP.Domain.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Pricing;

public sealed class PriceListCustomerConfiguration : IEntityTypeConfiguration<PriceListCustomer>
{
    public void Configure(EntityTypeBuilder<PriceListCustomer> builder)
    {
        builder.ToTable("price_list_customers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.PriceListId).HasColumnName("price_list_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<PriceList>()
            .WithMany()
            .HasForeignKey(x => x.PriceListId)
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerId es un Guid suelto sin FK física a BusinessPartner (módulo MasterData) —
        // mismo criterio que PriceListItem.ItemId: Pricing no debe depender del tipo
        // BusinessPartner ni de su módulo (ver PRICING-CUSTOMER-PRICE-LIST-FOUNDATION-05A).
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("ix_price_list_customers_customer");

        builder
            .HasIndex(x => new { x.PriceListId, x.CustomerId })
            .IsUnique()
            .HasDatabaseName("uq_price_list_customers_list_customer");

        // Regla de negocio: como máximo UNA relación ACTIVA por (Tenant, Company, Customer) —
        // garantizado en BD, mismo patrón que "uq_price_lists_tenant_company_default" para
        // PriceList.IsDefault.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.CustomerId,
                x.IsActive,
            })
            .IsUnique()
            .HasDatabaseName("uq_price_list_customers_tenant_company_customer_active")
            .HasFilter("is_active = true");
    }
}

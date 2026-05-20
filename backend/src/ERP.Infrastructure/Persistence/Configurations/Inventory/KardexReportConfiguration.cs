using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class KardexReportConfiguration : IEntityTypeConfiguration<KardexReport>
{
    public void Configure(EntityTypeBuilder<KardexReport> builder)
    {
        builder.ToTable("kardex_report");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(r => r.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(r => r.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(r => r.DateFrom).HasColumnName("date_from");
        builder.Property(r => r.DateTo).HasColumnName("date_to");
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(r => r.ResultJson).HasColumnName("result_json").HasColumnType("text");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(r => new { r.SubscriberId, r.Status }).HasDatabaseName("ix_kardex_report_subscriber_status");
        builder.HasIndex(r => r.RequestedAt).HasDatabaseName("ix_kardex_report_requested_at");
    }
}

using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class WithholdingCertConfiguration : IEntityTypeConfiguration<WithholdingCert>
{
    public void Configure(EntityTypeBuilder<WithholdingCert> builder)
    {
        builder.ToTable("withholding_cert");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id");
        builder.Property(x => x.SupplierRuc).HasColumnName("supplier_ruc").HasMaxLength(13).IsFixedLength().IsRequired();
        builder.Property(x => x.SupplierName).HasColumnName("supplier_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PurchaseInvId).HasColumnName("purchase_inv_id");
        builder.Property(x => x.TotalRetained).HasColumnName("total_retained").HasPrecision(18, 4).HasDefaultValue(0m);

        builder.HasIndex(x => new { x.CompanyId, x.SupplierId }).HasDatabaseName("idx_wc_supplier");
    }
}

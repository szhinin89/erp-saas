using ERP.Domain.Modules.Auxiliary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Auxiliary;

public class VatRefundConfiguration : IEntityTypeConfiguration<VatRefund>
{
    public void Configure(EntityTypeBuilder<VatRefund> builder)
    {
        builder.ToTable("vat_refund");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.SalesDocId).HasColumnName("sales_doc_id").IsRequired();
        builder.Property(x => x.RefundAmount).HasColumnName("refund_amount").HasPrecision(18, 4);
        builder.Property(x => x.AppliedDate).HasColumnName("applied_date");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(x => x.SriReference).HasColumnName("sri_reference").HasMaxLength(100);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("idx_vat_refund_company");
    }
}

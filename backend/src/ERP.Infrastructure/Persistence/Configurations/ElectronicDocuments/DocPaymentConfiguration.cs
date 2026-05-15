using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class DocPaymentConfiguration : IEntityTypeConfiguration<DocPayment>
{
    public void Configure(EntityTypeBuilder<DocPayment> builder)
    {
        builder.ToTable("doc_payment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(5).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
        builder.Property(x => x.PaymentTerm).HasColumnName("payment_term");
        builder.Property(x => x.Bank).HasColumnName("bank").HasMaxLength(100);
        builder.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(50);

        builder.HasIndex(x => x.DocId).HasDatabaseName("idx_doc_payment");

        builder.HasOne(x => x.Doc)
            .WithMany(x => x.Payments).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Method)
            .WithMany().HasForeignKey(x => x.PaymentMethod).OnDelete(DeleteBehavior.Restrict);
    }
}

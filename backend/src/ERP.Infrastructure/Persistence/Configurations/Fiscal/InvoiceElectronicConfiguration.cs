using ERP.Domain.Modules.Fiscal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Fiscal;

public sealed class InvoiceElectronicConfiguration : IEntityTypeConfiguration<InvoiceElectronic>
{
    public void Configure(EntityTypeBuilder<InvoiceElectronic> builder)
    {
        builder.ToTable("invoice_electronic");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(InvoiceElectronic.StatusMaxLen).IsRequired();
        builder.Property(e => e.AuthorizationNumber).HasColumnName("authorization_number").HasMaxLength(20);
        builder.Property(e => e.AuthorizationDate).HasColumnName("authorization_date");
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(InvoiceElectronic.PathMaxLen);
        builder.Property(e => e.XmlAuthorizedPath).HasColumnName("xml_authorized_path").HasMaxLength(InvoiceElectronic.PathMaxLen);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(InvoiceElectronic.ErrorMaxLen);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.InvoiceId }).IsUnique().HasDatabaseName("uq_invoice_electronic_invoice");
    }
}

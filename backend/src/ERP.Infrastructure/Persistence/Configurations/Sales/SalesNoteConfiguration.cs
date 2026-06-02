using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesNoteConfiguration : IEntityTypeConfiguration<SalesNote>
{
    public void Configure(EntityTypeBuilder<SalesNote> builder)
    {
        builder.ToTable("sales_note");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.OriginalBillId).HasColumnName("original_bill_id").IsRequired();
        builder.Property(e => e.NoteType)
            .HasColumnName("note_type")
            .HasMaxLength(10)
            .HasConversion(
                v => NoteTypeConversions.ToDb(v),
                v => NoteTypeConversions.FromDb(v))
            .IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(SalesNote.ReasonMaxLen).IsRequired();
        builder.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(SalesNote.DocTypeMaxLen).IsRequired();
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(SalesNote.EstabMaxLen).IsRequired();
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(SalesNote.EmPointMaxLen).IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(SalesNote.SequentialMaxLen).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(SalesNote.AccessKeyMaxLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(SalesNote.StatusMaxLen).IsRequired();
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(SalesNote.XmlPathMaxLen);
        builder.Property(e => e.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(SalesNote.XmlPathMaxLen);
        builder.Property(e => e.AuthNumber).HasColumnName("auth_number").HasMaxLength(64);
        builder.Property(e => e.AuthDate).HasColumnName("auth_date");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(SalesNote.ErrorMaxLen);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.OriginalBill);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.SalesNoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.EstabCode, e.EmPointCode, e.Sequential }).IsUnique().HasDatabaseName("uq_sales_note_seq");
        builder.HasIndex(e => new { e.SubscriberId, e.OriginalBillId }).HasDatabaseName("ix_sales_note_subscriber_bill");
    }
}

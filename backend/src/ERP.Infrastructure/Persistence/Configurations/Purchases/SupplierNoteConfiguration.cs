using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public class SupplierNoteConfiguration : IEntityTypeConfiguration<SupplierNote>
{
    public void Configure(EntityTypeBuilder<SupplierNote> builder)
    {
        builder.ToTable("supplier_note");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        builder.Property(x => x.NoteNumber).HasColumnName("note_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccessKey).HasColumnName("access_key").HasMaxLength(49).IsFixedLength();
        builder.Property(x => x.DocType).HasColumnName("doc_type").HasMaxLength(5).IsRequired();
        builder.Property(x => x.NoteDate).HasColumnName("note_date");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(300);
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder.HasIndex(x => new { x.CompanyId, x.BusinessPartnerId, x.NoteNumber })
            .IsUnique().HasDatabaseName("uq_sup_note");

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.SupplierNotes)
            .HasForeignKey(x => x.InvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

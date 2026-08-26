using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpenseDocumentConfiguration : IEntityTypeConfiguration<ExpenseDocument>
{
    public void Configure(EntityTypeBuilder<ExpenseDocument> builder)
    {
        builder.ToTable("expense_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.SupplierName)
            .HasColumnName("supplier_name")
            .HasMaxLength(ExpenseDocument.SupplierNameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SupplierTaxId)
            .HasColumnName("supplier_tax_id")
            .HasMaxLength(ExpenseDocument.SupplierTaxIdMaxLen)
            .IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(x => x.AccountingDate).HasColumnName("accounting_date").IsRequired();
        builder
            .Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(ExpenseDocument.DocumentTypeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(ExpenseDocument.DocumentNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AuthorizationNumber)
            .HasColumnName("authorization_number")
            .HasMaxLength(ExpenseDocument.AuthorizationNumberMaxLen);
        builder.Property(x => x.AuthorizationDate).HasColumnName("authorization_date");
        builder.Property(x => x.PaymentTermId).HasColumnName("payment_term_id").IsRequired();
        builder
            .Property(x => x.PaymentTermName)
            .HasColumnName("payment_term_name")
            .HasMaxLength(ExpenseDocument.PaymentTermNameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.PaymentTermInstallments)
            .HasColumnName("payment_term_installments")
            .IsRequired();
        builder
            .Property(x => x.PaymentTermDaysBetween)
            .HasColumnName("payment_term_days_between")
            .IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ExpenseDocument.NotesMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder
            .Property(x => x.ConfirmedSubtotal)
            .HasColumnName("confirmed_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedTotalTax)
            .HasColumnName("confirmed_total_tax")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedTotalDiscount)
            .HasColumnName("confirmed_total_discount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedGrandTotal)
            .HasColumnName("confirmed_grand_total")
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.TotalDiscount);
        builder.Ignore(x => x.TotalVat);
        builder.Ignore(x => x.TotalTax);
        builder.Ignore(x => x.GrandTotal);

        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.ExpenseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.PaymentSchedules)
            .WithOne()
            .HasForeignKey(x => x.ExpenseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<PaymentTerm>()
            .WithMany()
            .HasForeignKey(x => x.PaymentTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_expense_documents_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierId,
                x.DocumentType,
                x.DocumentNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_expense_documents_tenant_company_supplier_type_number");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.IssueDate })
            .HasDatabaseName("ix_expense_documents_tenant_company_issue_date");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.Status })
            .HasDatabaseName("ix_expense_documents_tenant_company_status");
    }
}

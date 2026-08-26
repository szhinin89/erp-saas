using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpensePaymentScheduleConfiguration
    : IEntityTypeConfiguration<ExpensePaymentSchedule>
{
    public void Configure(EntityTypeBuilder<ExpensePaymentSchedule> builder)
    {
        builder.ToTable("expense_payment_schedules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.ExpenseDocumentId)
            .HasColumnName("expense_document_id")
            .IsRequired();
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ExpensePaymentSchedule.NotesMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.ExpenseDocumentId })
            .HasDatabaseName("ix_expense_payment_schedules_tenant_document");

        builder
            .HasIndex(x => new { x.TenantId, x.DueDate })
            .HasDatabaseName("ix_expense_payment_schedules_tenant_duedate");

        builder
            .HasIndex(x => new { x.ExpenseDocumentId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_expense_payment_schedules_document_number");
    }
}

using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Accounting;

public sealed class AccountingSetupConfiguration : IEntityTypeConfiguration<AccountingSetup>
{
    public void Configure(EntityTypeBuilder<AccountingSetup> builder)
    {
        builder.ToTable("accounting_setup");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.InventoryAccountId).HasColumnName("inventory_account_id");
        builder.Property(e => e.CostOfSalesAccountId).HasColumnName("cost_of_sales_account_id");
        builder.Property(e => e.SuppliersAccountId).HasColumnName("suppliers_account_id");
        builder.Property(e => e.SalesAccountId).HasColumnName("sales_account_id");
        builder.Property(e => e.CustomersAccountId).HasColumnName("customers_account_id");
        builder.Property(e => e.VatPurchasesAccountId).HasColumnName("vat_purchases_account_id");
        builder.Property(e => e.VatSalesAccountId).HasColumnName("vat_sales_account_id");
        builder.Property(e => e.CashAccountId).HasColumnName("cash_account_id");
        builder.Property(e => e.BankAccountId).HasColumnName("bank_account_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.SubscriberId).IsUnique().HasDatabaseName("uq_accounting_setup_subscriber");

        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.InventoryAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CostOfSalesAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.SuppliersAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.SalesAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CustomersAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.VatPurchasesAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.VatSalesAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.CashAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

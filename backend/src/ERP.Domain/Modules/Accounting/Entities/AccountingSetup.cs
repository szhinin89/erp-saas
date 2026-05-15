using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

public sealed class AccountingSetup : AuditableEntity, ITenantEntity
{
    public Guid? InventoryAccountId    { get; private set; }
    public Guid? CostOfSalesAccountId  { get; private set; }
    public Guid? SuppliersAccountId    { get; private set; }
    public Guid? SalesAccountId        { get; private set; }
    public Guid? CustomersAccountId    { get; private set; }
    public Guid? VatPurchasesAccountId { get; private set; }
    public Guid? VatSalesAccountId     { get; private set; }
    public Guid? CashAccountId         { get; private set; }
    public Guid? BankAccountId         { get; private set; }

    private AccountingSetup() { }

    public static AccountingSetup Create(Guid tenantId, Guid createdBy)
    {
        var e = new AccountingSetup { TenantId = tenantId };
        e.SetCreated(createdBy);
        return e;
    }

    public void UpdateAccounts(
        Guid? inventoryAccountId,
        Guid? costOfSalesAccountId,
        Guid? suppliersAccountId,
        Guid? salesAccountId,
        Guid? customersAccountId,
        Guid? vatPurchasesAccountId,
        Guid? vatSalesAccountId,
        Guid? cashAccountId,
        Guid? bankAccountId,
        Guid  updatedBy)
    {
        InventoryAccountId    = inventoryAccountId;
        CostOfSalesAccountId  = costOfSalesAccountId;
        SuppliersAccountId    = suppliersAccountId;
        SalesAccountId        = salesAccountId;
        CustomersAccountId    = customersAccountId;
        VatPurchasesAccountId = vatPurchasesAccountId;
        VatSalesAccountId     = vatSalesAccountId;
        CashAccountId         = cashAccountId;
        BankAccountId         = bankAccountId;
        SetUpdated(updatedBy);
    }
}

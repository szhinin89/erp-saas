namespace ERP.Application.Modules.Accounting.DTOs;

public sealed record AccountingSetupDto(
    Guid? InventoryAccountId,
    Guid? CostOfSalesAccountId,
    Guid? SuppliersAccountId,
    Guid? SalesAccountId,
    Guid? CustomersAccountId,
    Guid? PurchasesVatAccountId,
    Guid? SalesVatAccountId,
    Guid? CashAccountId,
    Guid? BankAccountId);

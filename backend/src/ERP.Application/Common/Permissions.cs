namespace ERP.Application.Common;

/// <summary>
/// Centralized registry of all permission keys used in the system.
/// Format: {module}.{resource}.{action}
///
/// These keys are used in:
///   - Backend: [Authorize(Policy = "perm:{key}")]
///   - Backend: <see cref="ERP.Application.Access.Authorization.IRuntimePermissionAuthorizer"/> / <see cref="ERP.Application.Access.Caching.IEffectivePermissionKeysProvider"/>
///   - Frontend: usePermissionsStore.has(key)
///
/// Naming convention:
///   module   = top-level ERP area (sales, inventory, purchases, finance, admin, logistics)
///   resource = entity or sub-module
///   action   = view | create | update | delete | void | approve | cancel
/// </summary>
public static class Permissions
{
    // ── Sales ───────────────────────────────────────────────────────────────────
    public static class SalesQuote
    {
        public const string View   = "sales.quotes.view";
        public const string Create = "sales.quotes.create";
        public const string Update = "sales.quotes.update";
    }

    public static class SalesOrder
    {
        public const string View   = "sales.orders.view";
        public const string Create = "sales.orders.create";
        public const string Update = "sales.orders.update";
    }

    public static class SalesInvoice
    {
        public const string View   = "sales.invoices.view";
        public const string Create = "sales.invoices.create";
        public const string Update = "sales.invoices.update";
        public const string Void   = "sales.invoices.void";

        public static IReadOnlyList<string> All =>
            [View, Create, Update, Void];
    }

    public static class SalesCreditNote
    {
        public const string View   = "sales.credit-notes.view";
        public const string Create = "sales.credit-notes.create";
        public const string Send   = "sales.credit-notes.send";
        public const string Void   = "sales.credit-notes.void";
    }

    public static class SalesDebitNote
    {
        public const string View   = "sales.debit-notes.view";
        public const string Create = "sales.debit-notes.create";
    }

    public static class SalesCustomer
    {
        public const string View   = "sales.customers.view";
        public const string Create = "sales.customers.create";
        public const string Update = "sales.customers.update";
        public const string Delete = "sales.customers.delete";
    }

    // ── Inventory ───────────────────────────────────────────────────────────────
    public static class InventoryProduct
    {
        public const string View   = "inventory.products.view";
        public const string Create = "inventory.products.create";
        public const string Update = "inventory.products.update";
        public const string Delete = "inventory.products.delete";
    }

    public static class InventoryBrand
    {
        public const string View   = "inventory.brands.view";
        public const string Create = "inventory.brands.create";
        public const string Update = "inventory.brands.update";
        public const string Delete = "inventory.brands.delete";
    }

    public static class InventoryWarehouse
    {
        public const string View   = "inventory.warehouses.view";
        public const string Create = "inventory.warehouses.create";
        public const string Update = "inventory.warehouses.update";
    }

    public static class InventoryTransfer
    {
        public const string View    = "inventory.transfers.view";
        public const string Create  = "inventory.transfers.create";
        public const string Approve = "inventory.transfers.approve";
    }

    public static class InventoryAdjustment
    {
        public const string View    = "inventory.adjustments.view";
        public const string Create  = "inventory.adjustments.create";
        public const string Approve = "inventory.adjustments.approve";
    }

    // ── Purchases ───────────────────────────────────────────────────────────────
    public static class PurchaseOrder
    {
        public const string View    = "purchases.orders.view";
        public const string Create  = "purchases.orders.create";
        public const string Approve = "purchases.orders.approve";
        public const string Cancel  = "purchases.orders.cancel";
    }

    public static class PurchaseSupplier
    {
        public const string View   = "purchases.suppliers.view";
        public const string Create = "purchases.suppliers.create";
        public const string Update = "purchases.suppliers.update";
        public const string Delete = "purchases.suppliers.delete";
    }

    // ── Logistics ───────────────────────────────────────────────────────────────
    public static class LogisticsCarrier
    {
        public const string View   = "logistics.carriers.view";
        public const string Create = "logistics.carriers.create";
        public const string Update = "logistics.carriers.update";
        public const string Delete = "logistics.carriers.delete";
    }

    // ── Finance ─────────────────────────────────────────────────────────────────
    public static class FinanceConfig
    {
        public const string View = "finance.config.view";
        public const string Edit = "finance.config.edit";
    }

    public static class FinanceAccount
    {
        public const string View   = "finance.accounts.view";
        public const string Create = "finance.accounts.create";
        public const string Edit   = "finance.accounts.edit";
    }

    public static class FinanceJournal
    {
        public const string View = "finance.journal.view";
    }

    // ── MasterData ──────────────────────────────────────────────────────────────
    public static class MasterDataBusinessPartner
    {
        public const string View             = "masterdata.businesspartners.view";
        public const string Create           = "masterdata.businesspartners.create";
        public const string Update           = "masterdata.businesspartners.update";
        public const string Disable          = "masterdata.businesspartners.disable";
        public const string ConfigureCompany = "masterdata.businesspartners.configure-company";

        public static IReadOnlyList<string> All =>
            [View, Create, Update, Disable, ConfigureCompany];
    }

    // ── Admin ────────────────────────────────────────────────────────────────────
    public static class AdminRole
    {
        public const string View = "admin.roles.view";
    }

    public static class AdminUser
    {
        public const string View = "admin.users.view";
    }

    // ── Predefined role bundles ──────────────────────────────────────────────────

    /// <summary>All permissions granted to a "Facturador" (billing operator).</summary>
    public static IReadOnlyList<string> FacilitadorProfile =>
    [
        SalesQuote.View,
        SalesQuote.Create,
        SalesQuote.Update,
        SalesOrder.View,
        SalesOrder.Create,
        SalesOrder.Update,
        SalesInvoice.View,
        SalesInvoice.Create,
        SalesInvoice.Update,
        SalesInvoice.Void,
        SalesCreditNote.View,
        SalesCreditNote.Create,
        SalesCreditNote.Send,
        SalesCustomer.View,
        SalesCustomer.Create,
        SalesCustomer.Update,
        InventoryProduct.View,
    ];

    /// <summary>All permissions granted to a "Bodeguero" (warehouse operator).</summary>
    public static IReadOnlyList<string> BodegueroProfile =>
    [
        InventoryProduct.View,
        InventoryWarehouse.View,
        InventoryTransfer.View,
        InventoryTransfer.Create,
        InventoryAdjustment.View,
        InventoryAdjustment.Create,
        PurchaseOrder.View,
    ];

    /// <summary>All permissions granted to a "Contador" (accountant).</summary>
    public static IReadOnlyList<string> ContadorProfile =>
    [
        FinanceConfig.View,
        FinanceAccount.View,
        FinanceAccount.Create,
        FinanceAccount.Edit,
        FinanceJournal.View,
        SalesInvoice.View,
        PurchaseOrder.View,
    ];
}

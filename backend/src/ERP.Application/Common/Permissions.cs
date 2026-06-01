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
///   action   = view | create | update | delete | void | approve | cancel | execute | confirm
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

    /// <summary>Internal: check available stock when creating a sales invoice.</summary>
    public static class VentasStock
    {
        public const string View = "ventas.stock.view";
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

    public static class SalesWithholdingReceived
    {
        public const string View   = "sales.withholding-received.view";
        public const string Create = "sales.withholding-received.create";
    }

    /// <remarks>Legacy: superseded by <see cref="MasterDataBusinessPartner"/>. Keys kept for backward-compat with existing DB rows.</remarks>
    [Obsolete("Use MasterDataBusinessPartner instead. These keys map to the legacy sales.customers.* scope.")]
    public static class SalesCustomer
    {
        public const string View   = "sales.customers.view";
        public const string Create = "sales.customers.create";
        public const string Update = "sales.customers.update";
        public const string Delete = "sales.customers.delete";
    }

    // ── Inventory — Products ────────────────────────────────────────────────────
    public static class InventoryProduct
    {
        public const string View   = "inventory.products.view";
        public const string Create = "inventory.products.create";
        public const string Update = "inventory.products.update";
        public const string Delete = "inventory.products.delete";
    }

    // ── Inventory — Catalogs (read by forms/combos, write by admin) ─────────────
    public static class InventoryBrand
    {
        public const string View   = "inventory.brands.view";
        public const string Create = "inventory.brands.create";
        public const string Update = "inventory.brands.update";
        public const string Delete = "inventory.brands.delete";
    }

    public static class InventoryCategory
    {
        public const string View   = "inventory.categories.view";
        public const string Create = "inventory.categories.create";
        public const string Update = "inventory.categories.update";
        public const string Delete = "inventory.categories.delete";
    }

    public static class InventorySubcategory
    {
        public const string View   = "inventory.subcategories.view";
        public const string Create = "inventory.subcategories.create";
        public const string Update = "inventory.subcategories.update";
        public const string Delete = "inventory.subcategories.delete";
    }

    public static class InventoryProductLine
    {
        public const string View   = "inventory.product-lines.view";
        public const string Create = "inventory.product-lines.create";
        public const string Update = "inventory.product-lines.update";
        public const string Delete = "inventory.product-lines.delete";
    }

    public static class InventoryProductType
    {
        public const string View   = "inventory.product-types.view";
        public const string Create = "inventory.product-types.create";
        public const string Update = "inventory.product-types.update";
        public const string Delete = "inventory.product-types.delete";
    }

    public static class InventoryUnit
    {
        public const string View   = "inventory.units.view";
        public const string Create = "inventory.units.create";
        public const string Update = "inventory.units.update";
        public const string Delete = "inventory.units.delete";
    }

    public static class InventoryTariff
    {
        public const string View   = "inventory.tariffs.view";
        public const string Create = "inventory.tariffs.create";
    }

    /// <summary>
    /// Tax rates catalog. NOTE: key uses "inventario" (legacy Spanish prefix) for backward compat
    /// with existing DB permission rows. Do NOT rename without a DB migration.
    /// </summary>
    public static class InventoryTaxRate
    {
        public const string View = "inventario.taxRates.view";
    }

    // ── Inventory — Operations ──────────────────────────────────────────────────
    public static class InventoryWarehouse
    {
        public const string View   = "inventory.warehouses.view";
        public const string Create = "inventory.warehouses.create";
        public const string Update = "inventory.warehouses.update";
    }

    /// <summary>Current stock levels per product/warehouse.</summary>
    public static class InventoryStock
    {
        public const string View = "inventory.stock.view";
    }

    /// <summary>Stock movement history (Kardex).</summary>
    public static class InventoryKardex
    {
        public const string View = "inventory.kardex.view";
    }

    public static class InventoryTransfer
    {
        public const string View    = "inventory.transfers.view";
        public const string Create  = "inventory.transfers.create";
        /// <summary>Confirm (execute) a pending transfer — moves stock between warehouses.</summary>
        public const string Confirm = "inventory.transfers.confirm";
        public const string Cancel  = "inventory.transfers.cancel";

        /// <remarks>Dead constant — no controller uses this key. Use <see cref="Confirm"/> instead.</remarks>
        [Obsolete("No controller enforces this key. Use InventoryTransfer.Confirm = inventory.transfers.confirm")]
        public const string Approve = "inventory.transfers.approve";
    }

    public static class InventoryAdjustment
    {
        public const string View    = "inventory.adjustments.view";
        public const string Create  = "inventory.adjustments.create";
        /// <summary>Execute (apply) a pending adjustment — actually modifies stock.</summary>
        public const string Execute = "inventory.adjustments.execute";
        public const string Cancel  = "inventory.adjustments.cancel";

        /// <remarks>Dead constant — no controller uses this key. Use <see cref="Execute"/> instead.</remarks>
        [Obsolete("No controller enforces this key. Use InventoryAdjustment.Execute = inventory.adjustments.execute")]
        public const string Approve = "inventory.adjustments.approve";
    }

    // ── Purchases ───────────────────────────────────────────────────────────────
    public static class PurchaseOrder
    {
        public const string View    = "purchases.orders.view";
        public const string Create  = "purchases.orders.create";
        public const string Send    = "purchases.orders.send";
        public const string Approve = "purchases.orders.approve";
        public const string Cancel  = "purchases.orders.cancel";
        public const string LinkInvoice = "purchases.orders.link-invoice";
    }

    public static class PurchaseInvoice
    {
        public const string View   = "purchases.invoices.view";
        public const string Create = "purchases.invoices.create";
    }

    public static class PurchaseCreditNote
    {
        public const string View    = "purchases.credit-notes.view";
        public const string Create  = "purchases.credit-notes.create";
        public const string Approve = "purchases.credit-notes.approve";
    }

    public static class PurchaseWithholdingIssued
    {
        public const string View   = "purchases.withholding-issued.view";
        public const string Create = "purchases.withholding-issued.create";
        public const string Send   = "purchases.withholding-issued.send";
    }

    /// <remarks>Legacy: superseded by <see cref="MasterDataBusinessPartner"/>. Keys kept for backward-compat with existing DB rows.</remarks>
    [Obsolete("Use MasterDataBusinessPartner instead. These keys map to the legacy purchases.suppliers.* scope.")]
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
        public const string View   = "finance.journal.view";
        public const string Create = "finance.journal.create";
        public const string Edit   = "finance.journal.edit";
    }

    // ── Settings ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Covers: branches, establishments, emission points (all use perm:settings.branches.view).
    /// Needed by: Facturador (emit invoices), Bodeguero (select source establishment on transfers).
    /// </summary>
    public static class SettingsBranch
    {
        public const string View   = "settings.branches.view";
        public const string Create = "settings.branches.create";
        public const string Update = "settings.branches.update";
        public const string Delete = "settings.branches.delete";
    }

    public static class SettingsGeography
    {
        public const string View = "settings.geography.view";
    }

    public static class SettingsSri
    {
        public const string View = "settings.sri.view";
        public const string Edit = "settings.sri.edit";
    }

    public static class SettingsRide
    {
        public const string View = "settings.ride.view";
        public const string Edit = "settings.ride.edit";
    }

    // ── Cash / Banks ─────────────────────────────────────────────────────────────
    public static class CashBank
    {
        public const string View       = "cash.bank.view";
        public const string Create     = "cash.bank.create";
        public const string Conciliar  = "cash.bank.conciliar";
    }

    public static class CashPettyCash
    {
        public const string View    = "cash.bank.cajachica.view";
        public const string Create  = "cash.bank.cajachica.create";
        public const string Edit    = "cash.bank.cajachica.edit";
        public const string Arqueos = "cash.bank.arqueos.perform";
    }

    public static class CashFlow
    {
        public const string View = "cash.bank.flujo.view";
    }

    // ── Expenses ─────────────────────────────────────────────────────────────────
    public static class ExpenseInvoice
    {
        public const string View     = "expenses.invoices.view";
        public const string Create   = "expenses.invoices.create";
        public const string Validate = "expenses.invoices.validate";
        public const string Approve  = "expenses.invoices.approve";
        public const string Reject   = "expenses.invoices.reject";
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

    public static class AdminActivity
    {
        public const string View = "admin.activity.view";
    }

    // ── Access / Profiles ────────────────────────────────────────────────────────
    public static class AccessProfiles
    {
        public const string View = "access.profiles.view";
    }

    public static class AccessMemberships
    {
        public const string View = "access.company_user_memberships.view";
    }

    // ── SaaS (platform-level, not ERP) ───────────────────────────────────────────
    public static class SaasBilling
    {
        public const string View = "saas.billing.view";
    }

    // ── Predefined role bundles ──────────────────────────────────────────────────

    /// <summary>
    /// Permissions for "Facturador" (billing operator).
    /// Covers: full sales cycle + catalog read access for product forms + branch/warehouse context for invoices.
    /// </summary>
    public static IReadOnlyList<string> FacilitadorProfile =>
    [
        // Sales — full cycle
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
        // Clients/Suppliers master data
        MasterDataBusinessPartner.View,
        MasterDataBusinessPartner.Create,
        MasterDataBusinessPartner.Update,
        MasterDataBusinessPartner.Disable,
        // Products — view + catalog combos for forms
        InventoryProduct.View,
        InventoryBrand.View,
        InventoryCategory.View,
        InventorySubcategory.View,
        InventoryProductLine.View,
        InventoryProductType.View,
        InventoryUnit.View,
        InventoryTariff.View,
        InventoryTaxRate.View,
        // Stock — needed to validate availability when creating invoice
        InventoryStock.View,
        VentasStock.View,
        InventoryWarehouse.View,
        // Branches — required to select establishment / emission point on invoice
        SettingsBranch.View,
    ];

    /// <summary>
    /// Permissions for "Bodeguero" (warehouse operator).
    /// Covers: full stock lifecycle (adjustments + transfers) including execute/confirm actions,
    /// stock visibility, kardex, and catalog read access for product filter combos.
    /// </summary>
    public static IReadOnlyList<string> BodegueroProfile =>
    [
        // Products — view + filter combos
        InventoryProduct.View,
        InventoryBrand.View,
        InventoryCategory.View,
        // Warehouses
        InventoryWarehouse.View,
        // Stock level visibility
        InventoryStock.View,
        // Kardex (movement history)
        InventoryKardex.View,
        // Adjustments — full lifecycle: create → execute → (cancel if needed)
        InventoryAdjustment.View,
        InventoryAdjustment.Create,
        InventoryAdjustment.Execute,
        InventoryAdjustment.Cancel,
        // Transfers — full lifecycle: create → confirm → (cancel if needed)
        InventoryTransfer.View,
        InventoryTransfer.Create,
        InventoryTransfer.Confirm,
        InventoryTransfer.Cancel,
        // Purchase orders — read-only to receive against
        PurchaseOrder.View,
    ];

    /// <summary>
    /// Permissions for "Contador" (accountant).
    /// Covers: full accounting (read + create entries), sales/purchase read for reconciliation,
    /// expense management.
    /// </summary>
    public static IReadOnlyList<string> ContadorProfile =>
    [
        // Accounting — full access (read + write journal entries + account management)
        FinanceConfig.View,
        FinanceAccount.View,
        FinanceAccount.Create,
        FinanceAccount.Edit,
        FinanceJournal.View,
        FinanceJournal.Create,
        FinanceJournal.Edit,
        // Documents — read-only for reconciliation
        SalesInvoice.View,
        PurchaseOrder.View,
        PurchaseInvoice.View,
        // Business partners — read-only for document context
        MasterDataBusinessPartner.View,
        // Expenses — full lifecycle for accounting
        ExpenseInvoice.View,
        ExpenseInvoice.Create,
        ExpenseInvoice.Validate,
        ExpenseInvoice.Approve,
    ];
}

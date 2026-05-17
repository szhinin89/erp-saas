namespace ERP.Application.Common;

/// <summary>
/// Centralized registry of all permission keys used in the system.
/// Format: {module}.{resource}.{action}
///
/// These keys are used in:
///   - Backend: [Authorize(Policy = "perm:{key}")]
///   - Backend: PermissionHandler checks AccessProfilePermission.PermissionKey
///   - Frontend: usePermissionsStore.has(key)
///
/// Naming convention:
///   module   = top-level ERP area (ventas, inventario, compras, contabilidad, acceso, logistica)
///   resource = entity or sub-module (invoice, customer, product, carrier, ...)
///   action   = view | create | update | delete (maps to HTTP GET | POST | PUT | PATCH/DELETE)
/// </summary>
public static class Permissions
{
    // ── Sales / Ventas ──────────────────────────────────────────────────────────
    public static class Invoice
    {
        public const string View   = "ventas.invoice.view";
        public const string Create = "ventas.invoice.create";
        public const string Update = "ventas.invoice.update";
        public const string Void   = "ventas.invoice.void";

        public static IReadOnlyList<string> All =>
            [View, Create, Update, Void];
    }

    public static class CreditNote
    {
        public const string View   = "ventas.credit-note.view";
        public const string Create = "ventas.credit-note.create";
        public const string Void   = "ventas.credit-note.void";
    }

    public static class DebitNote
    {
        public const string View   = "ventas.debit-note.view";
        public const string Create = "ventas.debit-note.create";
    }

    public static class Customer
    {
        public const string View   = "ventas.customers.view";
        public const string Create = "ventas.customers.create";
        public const string Update = "ventas.customers.update";
        public const string Delete = "ventas.customers.delete";
    }

    // ── Inventory / Inventario ──────────────────────────────────────────────────
    public static class Product
    {
        public const string View   = "inventario.products.view";
        public const string Create = "inventario.products.create";
        public const string Update = "inventario.products.update";
        public const string Delete = "inventario.products.delete";
    }

    public static class Brand
    {
        public const string View   = "inventario.brands.view";
        public const string Create = "inventario.brands.create";
        public const string Update = "inventario.brands.update";
        public const string Delete = "inventario.brands.delete";
    }

    public static class Warehouse
    {
        public const string View   = "inventario.warehouses.view";
        public const string Create = "inventario.warehouses.create";
        public const string Update = "inventario.warehouses.update";
    }

    public static class StockTransfer
    {
        public const string View    = "inventario.transfers.view";
        public const string Create  = "inventario.transfers.create";
        public const string Approve = "inventario.transfers.approve";
    }

    public static class StockAdjustment
    {
        public const string View    = "inventario.adjustments.view";
        public const string Create  = "inventario.adjustments.create";
        public const string Approve = "inventario.adjustments.approve";
    }

    // ── Purchasing / Compras ────────────────────────────────────────────────────
    public static class PurchaseOrder
    {
        public const string View    = "compras.orders.view";
        public const string Create  = "compras.orders.create";
        public const string Approve = "compras.orders.approve";
        public const string Cancel  = "compras.orders.cancel";
    }

    public static class Supplier
    {
        public const string View   = "compras.proveedores.view";
        public const string Create = "compras.proveedores.create";
        public const string Update = "compras.proveedores.update";
        public const string Delete = "compras.proveedores.delete";
    }

    // ── Logistics / Logística ───────────────────────────────────────────────────
    public static class Carrier
    {
        public const string View   = "logistica.carriers.view";
        public const string Create = "logistica.carriers.create";
        public const string Update = "logistica.carriers.update";
        public const string Delete = "logistica.carriers.delete";
    }

    // ── Accounting / Contabilidad ───────────────────────────────────────────────
    public static class AccountingConfig
    {
        public const string View = "accounting.config.view";
        public const string Edit = "accounting.config.edit";
    }

    public static class Account
    {
        public const string View   = "accounting.accounts.view";
        public const string Create = "accounting.accounts.create";
        public const string Edit   = "accounting.accounts.edit";
    }

    public static class JournalEntry
    {
        public const string View = "accounting.journal.view";
    }

    // ── Access / Acceso ─────────────────────────────────────────────────────────
    public static class AccessProfile
    {
        public const string View = "access.profiles.view";
    }

    public static class Membership
    {
        public const string View = "access.memberships.view";
    }

    // ── Predefined role bundles ─────────────────────────────────────────────────

    /// <summary>All permissions granted to a "Facturador" (billing operator).</summary>
    public static IReadOnlyList<string> FacilitadorProfile =>
    [
        Invoice.View,
        Invoice.Create,
        Invoice.Update,
        Invoice.Void,
        CreditNote.View,
        CreditNote.Create,
        Customer.View,
        Customer.Create,
        Customer.Update,
        Product.View,
    ];

    /// <summary>All permissions granted to a "Bodeguero" (warehouse operator).</summary>
    public static IReadOnlyList<string> BodegueroProfile =>
    [
        Product.View,
        Warehouse.View,
        StockTransfer.View,
        StockTransfer.Create,
        StockAdjustment.View,
        StockAdjustment.Create,
        PurchaseOrder.View,
    ];

    /// <summary>All permissions granted to a "Contador" (accountant).</summary>
    public static IReadOnlyList<string> ContadorProfile =>
    [
        AccountingConfig.View,
        Account.View,
        Account.Create,
        Account.Edit,
        JournalEntry.View,
        Invoice.View,
        PurchaseOrder.View,
    ];
}

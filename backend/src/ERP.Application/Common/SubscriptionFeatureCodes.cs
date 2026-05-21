namespace ERP.Application.Common;

/// <summary>Códigos de features SaaS (catálogo). Al añadir módulos nuevos, definir aquí el código y el plan.</summary>
public static class SubscriptionFeatureCodes
{
    public const string Users = "USERS";
    public const string Documents = "DOCUMENTS";
    public const string Inventory = "INVENTORY";
    public const string Accounting = "ACCOUNTING";
    public const string Payroll = "PAYROLL";
    public const string ApiAccess = "API_ACCESS";
    public const string Access    = "ACCESS";
    public const string Customers = "CUSTOMERS";
    public const string Branches  = "BRANCHES";
    public const string Bodegas   = "BODEGAS";
    public const string Purchases = "PURCHASES";
    /// <summary>Alias legacy → <see cref="Purchases"/>.</summary>
    public const string Compras   = Purchases;
    public const string Sales     = "SALES";
    public const string Logistics = "LOGISTICS";
    public const string Expenses  = "EXPENSES";
    /// <summary>Alias legacy → <see cref="Expenses"/>.</summary>
    public const string Gastos    = Expenses;
}

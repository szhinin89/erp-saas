namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Perfil operativo inicial de una empresa nueva (IAM-V1-F3). Solo agrupa claves de permiso que ya
/// existen en <see cref="KernelRegistry.Permissions"/>; no crea permisos ni concede acceso
/// administrativo. Consumido por <c>AccessBootstrapStep</c>.
/// </summary>
public sealed record InitialProfileBundle(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions
);

/// <summary>
/// Perfiles «Facturador» y «Contador» que reciben únicamente las empresas NUEVAS. Después de la
/// creación el administrador de la empresa es el dueño de sus permisos: el bootstrap nunca los
/// repone ni los retira.
/// </summary>
public static class InitialProfileBundles
{
    public const string BillerName = "Facturador";
    public const string AccountantName = "Contador";

    /// <summary>
    /// Facturación y atención de clientes. Excluye cobros de cuentas por cobrar
    /// (<c>finance.create</c>) y todo <c>caja.*</c>: funciones que el Admin asigna aparte.
    /// </summary>
    public static InitialProfileBundle Biller { get; } =
        new(
            BillerName,
            "Facturación: ventas, clientes y consulta de catálogo, precios y existencias.",
            [
                SalesPermissions.View,
                SalesPermissions.Create,
                SalesPermissions.Update,
                MasterDataPermissions.BusinessPartnersView,
                MasterDataPermissions.BusinessPartnersCreate,
                MasterDataPermissions.BusinessPartnersUpdate,
                InventoryPermissions.ItemsView,
                PricingPermissions.View,
                InventoryPermissions.StockView,
                InventoryPermissions.WarehousesView,
                SettingsPermissions.BranchesView,
                MasterDataPermissions.PaymentTermsView,
                ElectronicDocumentsPermissions.View,
                ElectronicDocumentsPermissions.Detail,
                RidePermissions.View,
            ]
        );

    /// <summary>
    /// Consulta contable y asientos manuales (crear, editar, contabilizar). Excluye reversar y
    /// descartar asientos, crear/cerrar períodos, reprocesar contabilizaciones y editar el plan.
    /// </summary>
    public static InitialProfileBundle Accountant { get; } =
        new(
            AccountantName,
            "Contabilidad: consulta, reportes y asientos manuales.",
            [
                AccountingPermissions.View,
                AccountingPermissions.PostingsView,
                AccountingPermissions.ManualCreate,
                AccountingPermissions.ManualEdit,
                AccountingPermissions.ManualPost,
                SalesPermissions.View,
                PurchasePermissions.View,
                PayablesPermissions.View,
                FinancePermissions.View,
                ExpensePermissions.DocumentsView,
                ExpensePermissions.CatalogView,
                TreasuryPermissions.BankAccountsView,
                SettingsPermissions.BanksView,
                MasterDataPermissions.BusinessPartnersView,
                InventoryPermissions.ItemsView,
                ElectronicDocumentsPermissions.View,
                ElectronicDocumentsPermissions.Detail,
                RidePermissions.View,
            ]
        );

    public static IReadOnlyList<InitialProfileBundle> All { get; } = [Biller, Accountant];
}

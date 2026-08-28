namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// PAYABLES-READ-API-11 — permiso de solo lectura para la API genérica de Cuentas por Pagar
/// (<c>AccountsPayable</c>), distinto de <see cref="PurchasePermissions.View"/> porque cubre CxP
/// de cualquier origen (Compras y Gastos), no solo Compras. Sin <c>[NavItem]</c> deliberadamente:
/// esta API todavía no tiene una pantalla propia (ver alcance del ticket).
/// </summary>
public static class PayablesPermissions
{
    public const string View = "payables.view";
}

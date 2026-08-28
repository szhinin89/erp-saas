using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

/// <summary>
/// PAYABLES-FRONTEND-12 — módulo propio para la pantalla genérica de Cuentas por Pagar
/// (<c>/payables</c>, API <c>/api/v1/payables</c>), de solo lectura y que cubre Compras + Gastos
/// vía <c>AccountsPayable</c> — se le da su propio grupo en vez de anidarla bajo Compras o Gastos
/// porque no pertenece exclusivamente a ninguno de los dos.
/// PAYABLES-LEGACY-CLEANUP-13 eliminó el ítem "Cuentas por pagar" legacy que existía en
/// <c>PurchasesModule</c> (<c>/finance/payables</c>, solo Compras, con flujo de registro de pago,
/// también eliminado) — este es, desde entonces, el único NavItem de CxP.
/// </summary>
[Module("payables", Icon = "🧾", SortOrder = 48)]
public static class PayablesModule
{
    [NavItem(
        "Cuentas por pagar",
        Permission = PayablesPermissions.View,
        LabelKey = "app.nav.item.payables.list",
        SortOrder = 10
    )]
    public const string List = "/payables";
}

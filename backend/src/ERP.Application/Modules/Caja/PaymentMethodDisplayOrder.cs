using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Application.Modules.Caja;

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-UX-02/CASH-SESSION-LIST-SUMMARY-01 — orden fijo de UX para
/// mostrar formas de pago (Efectivo, Transferencia, Tarjeta, Cheque, Crédito; otros después),
/// distinto de <c>PaymentMethod.SortOrder</c> (orden de administración del catálogo). Deriva el
/// rango de flags ya cargados (<c>DetailType</c>/<c>IsCreditAllowed</c>/<c>AffectsPhysicalCash</c>)
/// — nunca compara por nombre, para no fusionar visualmente un <c>PaymentMethodId</c> huérfano/
/// legacy con el método vigente que tenga el mismo nombre (ver AUDIT-CASH-SESSION-COLLECTION-
/// SUMMARY-MISMATCH-01). Único punto de este cálculo — compartido por el resumen de un turno
/// (<c>GetCashSessionCollectionSummaryHandler</c>) y el listado de turnos
/// (<c>GetCashSessionListHandler</c>); nunca reimplementar el orden en otro lugar.
/// </summary>
internal static class PaymentMethodDisplayOrder
{
    /// <param name="method">null cuando el <c>PaymentMethodId</c> del pago ya no existe en el catálogo (huérfano/legacy) — cae en "otros".</param>
    public static int Rank(PaymentMethod? method)
    {
        if (method is null)
            return 6;
        if (method.IsCreditAllowed)
            return 5;
        return method.DetailType switch
        {
            PaymentMethodDetailType.Transfer => 2,
            PaymentMethodDetailType.Card => 3,
            PaymentMethodDetailType.Check => 4,
            PaymentMethodDetailType.None when method.AffectsPhysicalCash => 1,
            _ => 6,
        };
    }
}

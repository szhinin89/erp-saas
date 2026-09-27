namespace ERP.Domain.Kernel.Permissions;

public static class CajaPermissions
{
    public const string View = "caja.view";
    public const string Open = "caja.open";
    public const string Close = "caja.close";
    public const string Record = "caja.record";
    public const string Manage = "caja.manage";

    /// <summary>
    /// ZH-CASH-FUNDING-REQUEST-API-02E-D — consultar las solicitudes de efectivo de la sucursal activa
    /// (bandeja del cajero). Crear/cancelar la propia usa <c>supplier-payments.create</c>.
    /// </summary>
    public const string FundingRequestsView = "caja.funding-requests.view";

    /// <summary>
    /// ZH-CASH-FUNDING-REQUEST-API-02E-D — entregar/rechazar una solicitud. Nunca reemplaza el
    /// ownership: además hay que controlar la CashSession de la solicitud.
    /// </summary>
    public const string FundingRequestsFulfill = "caja.funding-requests.fulfill";
}

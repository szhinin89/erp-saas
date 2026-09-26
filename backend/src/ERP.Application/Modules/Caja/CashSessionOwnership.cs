using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Application.Modules.Caja;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B — mensajes de rechazo cuando el usuario actual no
/// controla una <see cref="CashSession"/> (regla SSOT: <see cref="CashSession.IsControlledBy"/>).
/// Un único lugar para que todos los flujos que tocan una sesión (pago a proveedor y su reversa,
/// reembolso de crédito de proveedor y su reversa, movimiento manual, cierre) respondan igual.
/// </summary>
/// <remarks>
/// La regla se aplica en Application y no dentro de <c>CashSession.RecordMovement</c>/<c>Close</c>
/// a propósito: flujos del sistema (venta autorizada, devolución de venta) también registran
/// movimientos y no son una acción del usuario sobre una sesión elegida.
/// </remarks>
public static class CashSessionOwnership
{
    public const string OperatedByAnotherUserMessage =
        "La caja seleccionada está siendo operada por otro usuario.";

    public const string NotOpenMessage = "La caja no está abierta.";

    /// <summary>Mensaje para una sesión que <paramref name="userId"/> no controla: cerrada, o abierta por otro usuario.</summary>
    public static string RejectionMessage(CashSession session) =>
        session.IsOpen ? OperatedByAnotherUserMessage : NotOpenMessage;
}

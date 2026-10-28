using System.Globalization;
using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Application.Modules.Caja;

/// <summary>
/// R3 (2026-10-28) — sin sobregiro de caja: una salida de efectivo que elige el cajero (egreso
/// manual, retiro, depósito bancario) nunca puede superar el efectivo esperado de la sesión
/// (<see cref="CashSession.CurrentBalance"/>, SSOT del arqueo). Misma regla que el pago a proveedor
/// en efectivo (02A-CLOSE en <c>SupplierPaymentRegistrar</c>); fail-closed, sin override. Se evalúa
/// con la sesión bloqueada (FOR UPDATE) para que dos salidas concurrentes no la superen juntas.
/// </summary>
/// <remarks>
/// Igual que <see cref="CashSessionOwnership"/>, vive en Application y no en <c>CashSession</c>: los
/// flujos del sistema (reembolso de devolución de venta, reembolso de saldo a favor) registran sus
/// propios movimientos y no son una salida elegida por el cajero.
/// </remarks>
public static class CashSessionAvailability
{
    /// <summary>Mensaje de rechazo si <paramref name="amount"/> supera el efectivo esperado; null si cabe.</summary>
    public static string? OverdraftMessage(CashSession session, decimal amount) =>
        amount > session.CurrentBalance
            ? $"La caja dispone de ${Money(session.CurrentBalance)} y se intenta registrar una salida de ${Money(amount)}."
            : null;

    private static string Money(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);
}

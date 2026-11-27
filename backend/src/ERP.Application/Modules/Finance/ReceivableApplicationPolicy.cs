using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Finance;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F4 (ADR-052 TA-12) — validaciones previas de una línea que reduce una
/// CxC, compartidas por el cobro (extraídas de <c>RegisterCollectionCommandHandler</c> sin cambiar su
/// comportamiento) y la aplicación de anticipos: la CxC existe en el alcance operativo, es del cliente
/// de la operación y la cuota indicada es suya y cubre el monto. Se evalúa con la CxC ya bloqueada.
/// Cancelación, saldo de la CxC y acumulado los decide <c>SalesReceivable.RegisterCollection</c>.
/// </summary>
internal static class ReceivableApplicationPolicy
{
    /// <summary><see cref="IsNotFound"/>: la CxC no existe en el alcance operativo (404).</summary>
    internal sealed record Rejection(bool IsNotFound, string Message);

    /// <param name="receivable">CxC bloqueada, o null si no se devolvió en el alcance.</param>
    /// <param name="operationLabel">Dueño de la operación en el mensaje, p. ej. «del cobro».</param>
    public static Rejection? Validate(
        SalesReceivable? receivable,
        Guid receivableId,
        Guid? installmentId,
        decimal amount,
        Guid customerId,
        string operationLabel
    )
    {
        if (receivable is null)
            return new(true, $"Cuenta por cobrar {receivableId} no encontrada.");

        if (receivable.CustomerId != customerId)
            return new(
                false,
                $"La cuenta por cobrar {receivableId} no pertenece al cliente {operationLabel}."
            );
        var installment = receivable.Installments.FirstOrDefault(i => i.Id == installmentId);
        if (installmentId is not null && installment is null)
            return new(
                false,
                $"La cuota {installmentId} no pertenece a la cuenta por cobrar {receivableId}."
            );
        // CXC-E2E-01 — lo aplicado se acumula en la cuota elegida: no puede exceder su saldo.
        if (installment is not null && amount > installment.OutstandingAmount)
            return new(
                false,
                $"El monto aplicado excede el saldo pendiente de la cuota #{installment.InstallmentNumber}."
            );
        return null;
    }
}

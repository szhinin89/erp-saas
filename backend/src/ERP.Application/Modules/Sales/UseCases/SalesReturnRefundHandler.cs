using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases;

/// <summary>
/// Ejecuta las <see cref="SalesReturnRefundAllocation"/> ya asignadas a una <see cref="SalesReturn"/>
/// (P0-01, Fase 6) — movimiento de Caja para <c>Cash</c>, crédito de CxC para
/// <c>ReceivableCredit</c>. Invocado exclusivamente de forma <b>síncrona</b> desde
/// <c>AuthorizeSalesReturnHandler</c>, dentro de la misma transacción que autoriza la devolución
/// — deliberadamente NO es un <c>INotificationHandler</c>: un reembolso de efectivo que falla no
/// puede quedar como un efecto colateral asíncrono desacoplado de la autorización (decisión de
/// arquitectura de esta fase). Registrado automáticamente en DI por convención (nombre termina en
/// "Handler" dentro de un namespace "UseCases" — ver <c>ERP.Application.DependencyInjection</c>).
/// </summary>
public sealed class SalesReturnRefundHandler
{
    private readonly ICashSessionRepository _cashRepo;
    private readonly ISalesReceivableRepository _receivableRepo;
    private readonly ICurrentCashSession _cashSession;

    public SalesReturnRefundHandler(
        ICashSessionRepository cashRepo,
        ISalesReceivableRepository receivableRepo,
        ICurrentCashSession cashSession
    )
    {
        _cashRepo = cashRepo;
        _receivableRepo = receivableRepo;
        _cashSession = cashSession;
    }

    /// <summary>
    /// Ejecuta cada <see cref="SalesReturn.RefundAllocations"/> ya presente en el agregado (Fase 5
    /// los agrega antes de llamar <c>Authorize()</c>). Devuelve <c>null</c> en éxito, o un mensaje
    /// de error listo para <c>Result.ValidationFailure</c> ante el primer fallo — el llamador es
    /// responsable de revertir toda la transacción (esta clase nunca hace commit/rollback).
    /// </summary>
    public async Task<string?> ExecuteAsync(
        SalesReturn salesReturn,
        Guid tenantId,
        Guid userId,
        CancellationToken ct
    )
    {
        foreach (var allocation in salesReturn.RefundAllocations)
        {
            var error = allocation.Method switch
            {
                SalesReturnRefundMethod.Cash => await ExecuteCashAsync(
                    salesReturn,
                    allocation,
                    tenantId,
                    userId,
                    ct
                ),
                SalesReturnRefundMethod.ReceivableCredit => await ExecuteReceivableCreditAsync(
                    salesReturn,
                    allocation,
                    tenantId,
                    userId,
                    ct
                ),
                _ => $"Forma de reembolso no soportada: '{allocation.Method}'.",
            };

            if (error is not null)
                return error;
        }

        return null;
    }

    private async Task<string?> ExecuteCashAsync(
        SalesReturn salesReturn,
        SalesReturnRefundAllocation allocation,
        Guid tenantId,
        Guid userId,
        CancellationToken ct
    )
    {
        // Reembolsa contra la sesión de caja ABIERTA actual (quien procesa la devolución), nunca
        // contra la sesión (probablemente ya cerrada) que originó la venta — a diferencia de
        // SaleIncome, que sí usa SalesInvoice.CashSessionId porque ocurre en la misma transacción
        // de la venta.
        if (!_cashSession.HasOpenSession)
            return "No existe una caja abierta para procesar el reembolso en efectivo. Abra una sesión de caja antes de autorizar la devolución.";

        var session = await _cashRepo.GetByIdAsync(tenantId, _cashSession.CashSessionId!.Value, ct);
        if (session is null)
            return "La sesión de caja abierta no fue encontrada.";

        session.RecordMovement(
            CashMovementType.SaleRefund,
            allocation.Amount,
            $"Devolución {salesReturn.ReturnNumber}",
            userId,
            CashReferenceType.SalesReturn,
            salesReturn.Id,
            salesReturn.ReturnNumber
        );

        return null;
    }

    private async Task<string?> ExecuteReceivableCreditAsync(
        SalesReturn salesReturn,
        SalesReturnRefundAllocation allocation,
        Guid tenantId,
        Guid userId,
        CancellationToken ct
    )
    {
        var receivable = await _receivableRepo.GetByInvoiceIdAsync(
            tenantId,
            salesReturn.SalesInvoiceId,
            ct
        );
        if (receivable is null)
            return "No existe una cuenta por cobrar asociada a la factura de esta devolución.";

        try
        {
            receivable.ApplyReturnCredit(allocation.Amount, userId);
            receivable.RebuildInstallments();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        return null;
    }
}

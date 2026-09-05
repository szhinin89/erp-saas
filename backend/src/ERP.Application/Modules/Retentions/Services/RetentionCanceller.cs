using ERP.Application.Common;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 — operación interna reutilizable que anula un
/// <see cref="RetentionDocument"/> YA CARGADO por el llamador (no lo vuelve a consultar ni valida
/// que esté "activa" — esa responsabilidad es de quien orquesta:
/// <c>CancelRetentionHandler</c> para la anulación aislada, <c>CancelExpenseDocumentHandler</c> para
/// la anulación integrada al anular el gasto origen), y revierte su impacto en
/// <see cref="AccountsPayable"/> si corresponde (<see cref="AccountsPayable.ReverseRetention"/>) —
/// mismo patrón "staged, sin SaveChanges" que <see cref="IRetentionIssuer"/> (01D-1), en reversa.
///
/// Deliberadamente NO llama <c>SaveChangesAsync</c>/<c>IUnitOfWork</c> — solo mutaciones en memoria
/// sobre entidades ya trackeadas (<see cref="RetentionDocument.Cancel"/>,
/// <see cref="AccountsPayable.ReverseRetention"/>). Quien invoca esta operación decide cuándo
/// persistir, para poder incluir la anulación de la retención en su propia transacción/SaveChanges
/// (evita el doble SaveChanges que rompería la atomicidad de la anulación del gasto — ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Flujo desde Gastos").
///
/// Bloquea (en vez de reversar de forma insegura) cuando la CxP originada ya tiene pagos
/// registrados — mismo criterio que <see cref="AccountsPayable.Cancel"/> ya usa para bloquear la
/// anulación completa de la CxP cuando hay pagos: revertir la retención en ese escenario dejaría el
/// saldo pendiente inconsistente con los pagos ya reconocidos contra el proveedor.
///
/// PURCHASES-RETENTIONS-CANCEL-05D: generalizado a cualquier origen que tenga un mapeo conocido a
/// <see cref="AccountsPayableOriginType"/> — hoy <see cref="RetentionSourceDocumentType.ExpenseDocument"/>
/// y <see cref="RetentionSourceDocumentType.PurchaseInvoice"/> (mismo mapeo 1:1 ya usado por
/// <see cref="IRetentionIssuer"/> al emitir). <see cref="RetentionSourceDocumentType.Manual"/> sigue
/// sin CxP asociada (reservado, sin implementación) — se anula el <c>RetentionDocument</c> sin
/// intentar resolver ninguna cuenta por pagar.
/// </summary>
public interface IRetentionCanceller
{
    Task<Result<RetentionDocument>> CancelAsync(
        RetentionDocument document,
        string reason,
        Guid cancelledBy,
        CancellationToken ct = default
    );
}

public sealed class RetentionCanceller : IRetentionCanceller
{
    private readonly IAccountsPayableRepository _payableRepo;

    public RetentionCanceller(IAccountsPayableRepository payableRepo) => _payableRepo = payableRepo;

    /// <summary>
    /// PURCHASES-RETENTIONS-CANCEL-05D — mismo mapeo 1:1 que <see cref="RetentionIssuer"/> ya usa
    /// implícitamente al resolver la CxP por origen (ver <c>IssueRetentionUseCases.HandlePurchaseAsync</c>/
    /// <c>ConfirmExpenseDocumentHandler</c>). <c>Manual</c> no tiene <see cref="AccountsPayableOriginType"/>
    /// equivalente — nunca se inventa uno.
    /// </summary>
    private static bool TryResolveAccountsPayableOriginType(
        RetentionSourceDocumentType sourceType,
        out AccountsPayableOriginType originType
    )
    {
        switch (sourceType)
        {
            case RetentionSourceDocumentType.ExpenseDocument:
                originType = AccountsPayableOriginType.ExpenseDocument;
                return true;
            case RetentionSourceDocumentType.PurchaseInvoice:
                originType = AccountsPayableOriginType.PurchaseInvoice;
                return true;
            default:
                originType = default;
                return false;
        }
    }

    public async Task<Result<RetentionDocument>> CancelAsync(
        RetentionDocument document,
        string reason,
        Guid cancelledBy,
        CancellationToken ct = default
    )
    {
        // Localiza la CxP originada por el documento fuente de la retención (si la hay) ANTES de
        // mutar nada — así el bloqueo por pagos aplicados ocurre sin haber tocado el
        // RetentionDocument, evitando una reversa parcial/insegura (ver docs/decisions/
        // RETENTIONS-MODULE-DESIGN-01.md § "Impacto en CxP").
        AccountsPayable? payable = null;
        if (TryResolveAccountsPayableOriginType(document.SourceDocumentType, out var originType))
        {
            payable = await _payableRepo.GetByOriginAsync(
                document.TenantId,
                document.CompanyId,
                originType,
                document.SourceDocumentId,
                ct
            );

            // Mismo criterio que AccountsPayable.Cancel(): si ya hay pagos registrados, revertir la
            // retención dejaría el saldo pendiente inconsistente con lo ya reconocido al proveedor
            // — bloqueo explícito, nunca una reversa parcial.
            if (payable is not null && payable.RetainedAmount > 0 && payable.PaidAmount > 0)
                return Result<RetentionDocument>.ValidationFailure(
                    "No se puede anular la retención: la cuenta por pagar del documento origen ya tiene pagos aplicados."
                );

            // PURCHASES-RETENTIONS-CANCEL-05D — para PurchaseInvoice (donde la CxP siempre se crea
            // sincrónicamente al confirmar la compra, mucho antes de que exista la retención) no
            // encontrar ninguna AccountsPayable con un monto realmente retenido es una
            // inconsistencia de datos: se rechaza en vez de anular la retención dejando el pasivo
            // fiscal ya reflejado en CxP sin reversar — mismo criterio fail-closed que
            // IssueRetentionHandler ya usa al emitir. Para ExpenseDocument se mantiene el
            // comportamiento histórico (tolerante: "sin CxP asociada, solo cancela la retención",
            // ver RetentionCancellerTests.Sin_CxP_asociada_solo_cancela_la_retencion) — no se
            // endurece aquí para no romper la anulación de retenciones de Gastos ya en producción.
            if (
                payable is null
                && document.TotalRetained > 0
                && document.SourceDocumentType == RetentionSourceDocumentType.PurchaseInvoice
            )
                return Result<RetentionDocument>.ValidationFailure(
                    "No se encontró la cuenta por pagar asociada al documento origen. No se puede anular la retención de forma segura."
                );
        }

        try
        {
            document.Cancel(reason, cancelledBy);
        }
        catch (ArgumentException ex)
        {
            return Result<RetentionDocument>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RetentionDocument>.ValidationFailure(ex.Message);
        }

        if (payable is not null && payable.RetainedAmount > 0)
        {
            try
            {
                payable.ReverseRetention(cancelledBy);
            }
            catch (InvalidOperationException ex)
            {
                return Result<RetentionDocument>.ValidationFailure(ex.Message);
            }
        }

        return Result<RetentionDocument>.Success(document);
    }
}

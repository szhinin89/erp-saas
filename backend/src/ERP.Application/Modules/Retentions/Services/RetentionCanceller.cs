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
/// Solo integra con <see cref="AccountsPayable"/> cuando el origen de la retención es
/// <see cref="RetentionSourceDocumentType.ExpenseDocument"/> — únicos consumidor real en E1 (mismo
/// alcance deliberado que <see cref="IRetentionIssuer"/>, que tampoco generaliza a Compras).
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
        if (document.SourceDocumentType == RetentionSourceDocumentType.ExpenseDocument)
        {
            payable = await _payableRepo.GetByOriginAsync(
                document.TenantId,
                document.CompanyId,
                AccountsPayableOriginType.ExpenseDocument,
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

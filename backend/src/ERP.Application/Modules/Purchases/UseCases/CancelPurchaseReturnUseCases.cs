using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 10 — cancela un <c>PurchaseReturn</c> en <c>Draft</c> (sin reversas) o
/// <c>Authorized</c> (reversa de inventario + CxP + crédito de proveedor condicional +
/// contabilidad, todo o nada). Bajo Lock A siempre, Lock B solo si existe <c>SupplierCredit</c>
/// asociado (§15.4). Idempotente (diseño §16.1/§16.2, fila <c>Cancel</c>).
/// </summary>
public sealed record CancelPurchaseReturnCommand(
    Guid PurchaseReturnId,
    string Reason,
    Guid ClientRequestId
) : IRequest<Result<PurchaseReturnDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelPurchaseReturnValidator : AbstractValidator<CancelPurchaseReturnCommand>
{
    public CancelPurchaseReturnValidator()
    {
        RuleFor(x => x.PurchaseReturnId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la cancelación es obligatorio.");
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelPurchaseReturnHandler
    : IRequestHandler<CancelPurchaseReturnCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelPurchaseReturnHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        ISupplierCreditRepository creditRepo,
        IStockRepository stockRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _creditRepo = creditRepo;
        _stockRepo = stockRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        CancelPurchaseReturnCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // §16.1 fila Cancel: BeginTransaction → descubrimiento sin tracking del
        // PurchaseInvoiceId → Lock A → descubrimiento sin tracking del SupplierCredit asociado (si
        // existe) → Lock B → recarga autoritativa de ambos agregados (primera carga tracking,
        // garantizadamente fresca tras los locks).
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceId = await _returnRepo.GetPurchaseInvoiceIdAsync(
                tid,
                cmd.PurchaseReturnId,
                ct
            );
            if (purchaseInvoiceId is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");
            }

            await _returnRepo.AcquireFinancialLockAsync(tid, purchaseInvoiceId.Value, ct);

            var creditId = await _creditRepo.GetIdBySourcePurchaseReturnIdAsync(
                tid,
                cmd.PurchaseReturnId,
                ct
            );
            if (creditId is not null)
                await _creditRepo.AcquireLockAsync(tid, creditId.Value, ct);

            var purchaseReturn = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
            if (purchaseReturn is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");
            }

            // ── Idempotencia (§16.2) — verificada ANTES de las revalidaciones de negocio, mismo
            // criterio que AuthorizePurchaseReturnHandler (Fase 6): un reintento idempotente
            // legítimo debe retornar el snapshot ya confirmado sin volver a ejecutar reversas. ──
            if (purchaseReturn.Status == PurchaseReturnStatus.Cancelled)
            {
                await _uow.RollbackAsync(ct);
                if (purchaseReturn.CancelClientRequestId != cmd.ClientRequestId)
                    // PR-009
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "Esta devolución ya está cancelada."
                    );

                var expectedHash = ComputeCancelPayloadHash(
                    purchaseReturn.Id,
                    cmd.ClientRequestId,
                    cmd.Reason
                );
                return purchaseReturn.CancelRequestPayloadHash == expectedHash
                    ? Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn))
                    // PR-012
                    : Result<PurchaseReturnDto>.ValidationFailure(
                        "Ya existe una solicitud de cancelación con este identificador pero con datos distintos."
                    );
            }

            // ── PR-011 (§5.1 casos 6/7): crédito íntegro, sin aplicaciones ni reembolsos activos ──
            SupplierCredit? credit = null;
            if (creditId is not null)
            {
                credit = await _creditRepo.GetByIdAsync(tid, creditId.Value, ct);
                if (credit is not null && credit.AvailableAmount != credit.OriginalAmount)
                {
                    await _uow.RollbackAsync(ct);
                    // PR-011
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "No se puede cancelar esta devolución porque su crédito de proveedor ya tiene aplicaciones o reembolsos activos."
                    );
                }
            }

            var wasAuthorized = purchaseReturn.Status == PurchaseReturnStatus.Authorized;

            if (wasAuthorized)
            {
                var invoice = await _invoiceRepo.GetByIdAsync(
                    tid,
                    purchaseReturn.PurchaseInvoiceId,
                    ct
                );
                if (invoice is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseReturnDto>.NotFound(
                        "Factura de compra de origen no encontrada."
                    );
                }

                var payable = await _payableRepo.GetByOriginAsync(
                    tid,
                    invoice.CompanyId,
                    AccountsPayableOriginType.PurchaseInvoice,
                    invoice.Id,
                    ct
                );
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseReturnDto>.NotFound(
                        "Cuenta por pagar de la factura de compra no encontrada."
                    );
                }

                // ── Reversa de inventario: movimiento inverso (+Quantity), mismo UnitCost, misma
                // bodega (§9.1, diseño Fase 10) ──
                foreach (var line in purchaseReturn.Lines)
                {
                    var originalLine = invoice.Lines.FirstOrDefault(l =>
                        l.Id == line.OriginalInvoiceDetailId
                    );
                    await _stockRepo.AppendMovementAsync(
                        tid,
                        purchaseReturn.CompanyId,
                        line.ItemId,
                        line.WarehouseId,
                        StockMovementType.PurchaseReturn,
                        line.Quantity,
                        originalLine?.UomCode ?? "UNIT",
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        $"ANULACIÓN devolución {purchaseReturn.ReturnNumber}",
                        purchaseReturn.Id,
                        "PurchaseReturn",
                        uid,
                        line.UnitCost,
                        sourceDocLineId: line.Id,
                        cancellationToken: ct
                    );
                }

                if (purchaseReturn.AppliedToPayableAmount is > 0m)
                    payable.ReverseReturnCredit(purchaseReturn.AppliedToPayableAmount.Value, uid);

                if (credit is not null)
                {
                    var movementHash = ComputeSourceReturnCancellationHash(
                        credit.Id,
                        purchaseReturn.Id,
                        cmd.ClientRequestId
                    );
                    try
                    {
                        credit.RegisterSourceReturnCancellation(
                            uid,
                            cmd.ClientRequestId,
                            movementHash
                        );
                    }
                    catch (InvalidOperationException ex)
                    {
                        await _uow.RollbackAsync(ct);
                        // PR-011 (defensa en profundidad del dominio)
                        return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
                    }
                }
            }

            var cancelHash = ComputeCancelPayloadHash(
                purchaseReturn.Id,
                cmd.ClientRequestId,
                cmd.Reason
            );
            try
            {
                purchaseReturn.Cancel(cmd.Reason, uid, cmd.ClientRequestId, cancelHash);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // PR-009
                return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
            }

            try
            {
                if (wasAuthorized)
                    await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);
                else
                    await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                // §16.2bis — colisión de CancelClientRequestId por una causa distinta al lock
                // (ventana estructuralmente pequeña: Lock A ya serializa Cancel sobre la misma
                // factura; solo ocurriría por reutilización cruzada del mismo ClientRequestId
                // contra un PurchaseReturnId distinto). Mismo patrón exacto que
                // AuthorizePurchaseReturnHandler (Fase 6).
                await _uow.RollbackAsync(ct);
                _uow.ClearChangeTracker();

                await _uow.BeginTransactionAsync(ct);
                try
                {
                    var reloaded = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
                    await _uow.CommitAsync(ct); // solo lectura, nada que persistir

                    if (
                        reloaded is not null
                        && reloaded.Status == PurchaseReturnStatus.Cancelled
                        && reloaded.CancelClientRequestId == cmd.ClientRequestId
                    )
                        return Result<PurchaseReturnDto>.Success(Map.ToDto(reloaded));

                    // PR-012
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "Ya existe una solicitud de cancelación con este identificador pero con datos distintos."
                    );
                }
                catch
                {
                    await _uow.RollbackAsync(ct);
                    throw;
                }
            }

            await _uow.CommitAsync(ct);

            return Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Huella determinista (§16.2, diseño línea 1012): OperationType+PurchaseReturnId+ClientRequestId+CancellationReason normalizada.</summary>
    internal static string ComputeCancelPayloadHash(
        Guid purchaseReturnId,
        Guid clientRequestId,
        string reason
    )
    {
        var normalizedReason = string.Join(
            " ",
            reason.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        var canonical = string.Join(
            "",
            "CancelPurchaseReturn",
            purchaseReturnId.ToString("D"),
            clientRequestId.ToString("D"),
            normalizedReason
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Huella determinista del movimiento <c>SourceReturnCancelled</c> — generado automáticamente
    /// por este handler, nunca por el usuario (§9.3), reutiliza el mismo <c>ClientRequestId</c>
    /// del comando de cancelación (ya garantiza unicidad por operación, la propia idempotencia de
    /// <c>PurchaseReturn.Cancel</c> impide que este código se ejecute dos veces con éxito).
    /// </summary>
    private static string ComputeSourceReturnCancellationHash(
        Guid supplierCreditId,
        Guid purchaseReturnId,
        Guid clientRequestId
    )
    {
        var canonical = string.Join(
            "",
            "SourceReturnCancellation",
            supplierCreditId.ToString("D"),
            purchaseReturnId.ToString("D"),
            clientRequestId.ToString("D")
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

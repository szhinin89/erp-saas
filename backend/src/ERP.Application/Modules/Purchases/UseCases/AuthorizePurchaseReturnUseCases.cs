using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
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
/// P0-02 Fase 6 — autoriza un <c>PurchaseReturn</c> en <c>Draft</c>: flujo atómico completo
/// (secuencia + inventario + CxP + crédito de proveedor condicional + contabilidad + auditoría),
/// bajo Lock A, idempotente (diseño §16.1/§16.2, fila <c>Authorize</c>).
/// </summary>
public sealed record AuthorizePurchaseReturnCommand(Guid PurchaseReturnId, Guid ClientRequestId)
    : IRequest<Result<PurchaseReturnDto>>,
        IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class AuthorizePurchaseReturnValidator
    : AbstractValidator<AuthorizePurchaseReturnCommand>
{
    public AuthorizePurchaseReturnValidator()
    {
        RuleFor(x => x.PurchaseReturnId).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class AuthorizePurchaseReturnHandler
    : IRequestHandler<AuthorizePurchaseReturnCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseReturnSequenceRepository _sequenceRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public AuthorizePurchaseReturnHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReturnSequenceRepository sequenceRepo,
        IStockRepository stockRepo,
        ISupplierCreditRepository creditRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _sequenceRepo = sequenceRepo;
        _stockRepo = stockRepo;
        _creditRepo = creditRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        AuthorizePurchaseReturnCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // §16.1 fila Authorize: BeginTransaction → descubrimiento sin tracking del
        // PurchaseInvoiceId (nunca rastrea PurchaseReturn antes del lock, mismo patrón que
        // IPurchasePayableRepository.GetPurchaseInvoiceIdAsync, Fase 3 Remediación Transaccional
        // 02) → Lock A → recarga autoritativa (primera carga tracking, garantizadamente fresca).
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

            var purchaseReturn = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
            if (purchaseReturn is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");
            }

            // ── Idempotencia (§16.2) — verificada inmediatamente tras la recarga, ANTES de las
            // revalidaciones de negocio (desviación deliberada respecto al orden literal
            // "revalidar → idempotencia" de §16.1/el encargo — ver informe final): si esta
            // devolución ya está Authorized, revalidar remanente contaría sus propias líneas como
            // "ya devueltas" (autoconteo, §10.2) y PurchaseReturn.Authorize() lanzaría por
            // EnsureDraft() al reintentar sobre un agregado que ya transicionó. Un reintento
            // idempotente legítimo (mismo ClientRequestId) debe retornar el snapshot ya
            // confirmado sin volver a ejecutar ningún efecto — nunca ser rechazado por sus propias
            // revalidaciones. ──
            if (purchaseReturn.Status == PurchaseReturnStatus.Authorized)
            {
                await _uow.RollbackAsync(ct);
                return purchaseReturn.AuthorizeClientRequestId == cmd.ClientRequestId
                    ? Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn))
                    // PR-012
                    : Result<PurchaseReturnDto>.ValidationFailure(
                        "Ya existe una solicitud de autorización con este identificador pero con datos distintos."
                    );
            }
            if (purchaseReturn.Status == PurchaseReturnStatus.Cancelled)
            {
                await _uow.RollbackAsync(ct);
                // PR-009
                return Result<PurchaseReturnDto>.ValidationFailure(
                    "Esta devolución ya fue cancelada."
                );
            }

            var invoice = await _invoiceRepo.GetByIdAsync(
                tid,
                purchaseReturn.PurchaseInvoiceId,
                ct
            );
            if (invoice is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseReturnDto>.NotFound("Factura de compra no encontrada.");
            }

            var payable = await _invoiceRepo.GetPayableByPurchaseIdAsync(tid, invoice.Id, ct);
            if (payable is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseReturnDto>.NotFound(
                    "Cuenta por pagar de la factura de compra no encontrada."
                );
            }

            var withholding = await _invoiceRepo.GetWithholdingByPurchaseIdAsync(
                tid,
                invoice.Id,
                ct
            );
            var hasIssuedWithholding =
                withholding is not null && withholding.Status == WithholdingStatus.Issued;

            // ── PR-004 revalidado bajo lock (§10.2) + snapshot de línea original para Authorize() ──
            var detailIds = purchaseReturn.Lines.Select(l => l.OriginalInvoiceDetailId).ToList();
            var returnedByDetailId = await _returnRepo.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                tid,
                detailIds,
                ct
            );

            var originalLinesByDetailId =
                new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>();
            var uomCodeByDetailId = new Dictionary<Guid, string>();
            foreach (var line in purchaseReturn.Lines)
            {
                var originalLine = invoice.Lines.FirstOrDefault(l =>
                    l.Id == line.OriginalInvoiceDetailId
                );
                if (originalLine is null)
                {
                    await _uow.RollbackAsync(ct);
                    // PR-003
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "La línea indicada no pertenece a la factura seleccionada."
                    );
                }

                var alreadyReturned = returnedByDetailId.GetValueOrDefault(originalLine.Id);
                var remaining = originalLine.Quantity - alreadyReturned;
                if (line.Quantity > remaining)
                {
                    await _uow.RollbackAsync(ct);
                    // PR-004
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        $"Línea '{originalLine.Description}': la cantidad solicitada ({line.Quantity}) "
                            + $"excede el remanente devolvible ({remaining})."
                    );
                }

                originalLinesByDetailId[originalLine.Id] = new PurchaseReturn.OriginalLineSnapshot(
                    originalLine.Quantity,
                    originalLine.LineSubtotal,
                    originalLine.DiscountAmount,
                    originalLine.VatAmount,
                    originalLine.IceAmount,
                    originalLine.VatCode,
                    originalLine.VatRate,
                    originalLine.IceCode,
                    originalLine.IceRate,
                    originalLine.LandedUnitCost
                );
                uomCodeByDetailId[line.OriginalInvoiceDetailId] = originalLine.UomCode;
            }

            // ── PR-005: stock suficiente en la bodega original de cada línea (§14.2) — chequeo
            // preventivo antes de consumir un número de secuencia; CurrentStock.ApplyMovement
            // (dentro de AppendMovementAsync, más abajo) es la defensa autoritativa final. ──
            foreach (var line in purchaseReturn.Lines)
            {
                var stock = await _stockRepo.GetStockAsync(tid, line.WarehouseId, line.ItemId, ct);
                var availableQty = stock?.Quantity ?? 0m;
                if (availableQty < line.Quantity)
                {
                    await _uow.RollbackAsync(ct);
                    // PR-005
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        $"Stock insuficiente en la bodega original para la línea (existencia: {availableQty}, "
                            + $"solicitado: {line.Quantity})."
                    );
                }
            }

            var returnNumber = await _sequenceRepo.CaptureNextAsync(
                tid,
                purchaseReturn.CompanyId,
                ct
            );
            var authorizeHash = ComputeAuthorizePayloadHash(purchaseReturn.Id, cmd.ClientRequestId);
            var balanceDueBeforeApplication = payable.BalanceDue;

            SupplierCredit? credit;
            try
            {
                credit = purchaseReturn.Authorize(
                    returnNumber,
                    originalLinesByDetailId,
                    balanceDueBeforeApplication,
                    invoice.CurrencyCode,
                    hasIssuedWithholding,
                    uid,
                    cmd.ClientRequestId,
                    authorizeHash
                );
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // PR-006 (retención Issued) u otro guard de dominio
                return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
            }

            try
            {
                foreach (var line in purchaseReturn.Lines)
                {
                    await _stockRepo.AppendMovementAsync(
                        tid,
                        purchaseReturn.CompanyId,
                        line.ItemId,
                        line.WarehouseId,
                        StockMovementType.PurchaseReturn,
                        -line.Quantity,
                        uomCodeByDetailId[line.OriginalInvoiceDetailId],
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        $"Devolución {purchaseReturn.ReturnNumber}",
                        purchaseReturn.Id,
                        "PurchaseReturn",
                        uid,
                        line.UnitCost,
                        sourceDocLineId: line.Id,
                        cancellationToken: ct
                    );
                }
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // PR-005 (defensa autoritativa de CurrentStock.ApplyMovement)
                return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
            }

            payable.ApplyReturnCredit(purchaseReturn.AuthorizedGrandTotal!.Value, uid);

            if (credit is not null)
                await _creditRepo.AddAsync(credit, ct);

            try
            {
                await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                // §16.2bis — colisión de AuthorizeClientRequestId por una causa distinta al lock
                // (ventana estructuralmente pequeña: Lock A ya serializa Authorize sobre la misma
                // factura; solo ocurriría por reutilización cruzada del mismo ClientRequestId
                // contra un PurchaseReturnId distinto). El DbContext con la transacción abortada
                // no es reutilizable — rollback + limpieza de ChangeTracker + transacción nueva
                // para reconsultar el estado real.
                await _uow.RollbackAsync(ct);
                _uow.ClearChangeTracker();

                await _uow.BeginTransactionAsync(ct);
                try
                {
                    var reloaded = await _returnRepo.GetByIdAsync(tid, cmd.PurchaseReturnId, ct);
                    await _uow.CommitAsync(ct); // solo lectura, nada que persistir

                    if (
                        reloaded is not null
                        && reloaded.Status == PurchaseReturnStatus.Authorized
                        && reloaded.AuthorizeClientRequestId == cmd.ClientRequestId
                    )
                        return Result<PurchaseReturnDto>.Success(Map.ToDto(reloaded));

                    // PR-012
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "Ya existe una solicitud de autorización con este identificador pero con datos distintos."
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

    /// <summary>Huella determinista (§16.2): <c>OperationType</c> + <c>PurchaseReturnId</c> + <c>ClientRequestId</c> — Authorize no recibe payload de negocio adicional.</summary>
    internal static string ComputeAuthorizePayloadHash(Guid purchaseReturnId, Guid clientRequestId)
    {
        var canonical = string.Join(
            "",
            "AuthorizePurchaseReturn",
            purchaseReturnId.ToString("D"),
            clientRequestId.ToString("D")
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Exceptions;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// Anula una compra confirmada. <paramref name="RequestSriAnnulment"/> (ZH-RETENTION-SRI-ANNULMENT-01):
/// si su retención ya está AUTORIZADA por el SRI, la anulación local se bloquea
/// (<c>ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT</c>); con <c>true</c> el usuario confirma que quiere
/// iniciar el trámite de anulación ante el SRI: se registra la solicitud (con este motivo) y la
/// compra sigue CONFIRMADA hasta que el SRI confirme ANULADO — recién entonces se anula por este
/// mismo flujo, una sola vez.
/// </summary>
public sealed record CancelPurchaseCommand(
    Guid PurchaseInvoiceId,
    string Reason,
    bool RequestSriAnnulment = false
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

/// <summary>
/// Contexto explícito de una anulación de compra. Desde la API se arma con el contexto autenticado
/// (<see cref="RequiredBranchId"/> = sucursal activa); la finalización de una anulación SRI
/// confirmada lo arma desde la solicitud registrada (sin sucursal activa: la compra ya fue validada
/// cuando se solicitó).
/// </summary>
public sealed record PurchaseCancellationContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PurchaseInvoiceId,
    string Reason,
    Guid UserId,
    Guid? RequiredBranchId,
    bool RequestSriAnnulment
);

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelPurchaseValidator : AbstractValidator<CancelPurchaseCommand>
{
    public CancelPurchaseValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.CancelReasonMaxLen)
            .WithMessage("El motivo de anulación es obligatorio (máximo 500 caracteres).");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelPurchaseHandler
    : IRequestHandler<CancelPurchaseCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IRetentionDocumentRepository _retentionRepo;
    private readonly IRetentionCanceller _retentionCanceller;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CancelPurchaseHandler> _logger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICompanyClock _companyClock;
    private readonly IPurchaseReceptionDocumentRepository? _receptionRepo;
    private readonly IRetentionAnnulmentRequester? _annulments;

    public CancelPurchaseHandler(
        IPurchaseInvoiceRepository repo,
        IAccountsPayableRepository payableRepo,
        IStockRepository stockRepo,
        IItemRepository itemRepo,
        IPurchaseReturnRepository purchaseReturnRepo,
        IRetentionDocumentRepository retentionRepo,
        IRetentionCanceller retentionCanceller,
        IUnitOfWork uow,
        ILogger<CancelPurchaseHandler> logger,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyClock companyClock,
        IPurchaseReceptionDocumentRepository? receptionRepo = null,
        IRetentionAnnulmentRequester? annulments = null
    )
    {
        _annulments = annulments;
        _repo = repo;
        _payableRepo = payableRepo;
        _stockRepo = stockRepo;
        _itemRepo = itemRepo;
        _purchaseReturnRepo = purchaseReturnRepo;
        _retentionRepo = retentionRepo;
        _retentionCanceller = retentionCanceller;
        _uow = uow;
        _logger = logger;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _companyClock = companyClock;
        _receptionRepo = receptionRepo;
    }

    public Task<Result<PurchaseInvoiceDto>> Handle(
        CancelPurchaseCommand cmd,
        CancellationToken ct
    ) =>
        ExecuteAsync(
            new PurchaseCancellationContext(
                _t.TenantId,
                _c.CompanyId,
                cmd.PurchaseInvoiceId,
                cmd.Reason,
                _u.UserId,
                _b.BranchId,
                cmd.RequestSriAnnulment
            ),
            ct
        );

    /// <summary>
    /// Flujo oficial único de anulación de compra (API y finalización de una anulación SRI
    /// confirmada). Todos los guards, locks y efectos viven aquí — la finalización no los duplica.
    /// </summary>
    public async Task<Result<PurchaseInvoiceDto>> ExecuteAsync(
        PurchaseCancellationContext ctx,
        CancellationToken ct
    )
    {
        var tid = ctx.TenantId;
        var cid = ctx.CompanyId;
        var uid = ctx.UserId;
        var cmd = new CancelPurchaseCommand(
            ctx.PurchaseInvoiceId,
            ctx.Reason,
            ctx.RequestSriAnnulment
        );

        // Fase 3 (P0-02, Remediación transaccional 02) — cmd.PurchaseInvoiceId ya identifica
        // directamente qué Lock A adquirir: no se requiere ninguna carga de descubrimiento.
        // PurchaseInvoice se carga por primera vez YA BAJO el lock (recarga autoritativa real,
        // nunca la misma instancia servida por el identity map de EF Core).
        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _purchaseReturnRepo.AcquireFinancialLockAsync(tid, cmd.PurchaseInvoiceId, ct);

            // ── 1. Cargar y validar (recarga autoritativa bajo lock) ───────
            var inv = await _repo.GetByIdAsync(tid, cmd.PurchaseInvoiceId, ct);
            if (inv is null || (ctx.RequiredBranchId is Guid branchId && inv.BranchId != branchId))
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
            }

            _logger.LogInformation(
                "Cancelling purchase {InvoiceNumber} ({InvoiceId}) for tenant {TenantId}. Reason: {Reason}",
                inv.InvoiceNumber,
                inv.Id,
                tid,
                cmd.Reason
            );

            // ── 1b. Cargar cuenta por pagar (recargada bajo lock) y bloquear si hay pagos ──
            var payable = await _payableRepo.GetByOriginAsync(
                tid,
                cid,
                AccountsPayableOriginType.PurchaseInvoice,
                inv.Id,
                ct
            );
            var eligibility = await new PurchaseCancellationEligibility(_stockRepo, _purchaseReturnRepo)
                .EvaluateAsync(inv, payable, ct);
            if (!eligibility.IsSuccess)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseInvoiceDto>.ValidationFailure(eligibility.Error!, eligibility.Code);
            }

            // Gate fiscal único ADR-036: solo la retención emitida por nuestra empresa.
            // La factura recibida pertenece al proveedor; Cancel local no solicita su anulación SRI.
            var retention = await _retentionRepo.GetBySourceAsync(
                tid,
                cid,
                RetentionSourceDocumentType.PurchaseInvoice,
                inv.Id,
                ct
            );
            if (retention is not null && retention.Status == RetentionStatus.Issued)
            {
                var cancelRetentionResult = await _retentionCanceller.CancelAsync(
                    retention,
                    "Anulación automática por anulación de compra.",
                    uid,
                    ct
                );
                if (!cancelRetentionResult.IsSuccess)
                {
                    // ZH-RETENTION-SRI-ANNULMENT-01 — retención AUTORIZADA y el usuario pidió iniciar
                    // la anulación ante el SRI: se registra la solicitud (bajo los mismos locks y
                    // guards de esta anulación) y la compra sigue confirmada. Sin reversos.
                    if (
                        ctx.RequestSriAnnulment
                        && cancelRetentionResult.Code
                            == ApiResponseCodes
                                .ElectronicDocuments
                                .SourceCancellationRequiresSriAnnulment
                    )
                        return await RequestSriAnnulmentAsync(inv, retention, ctx, ct);

                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseInvoiceDto>.ValidationFailure(
                        cancelRetentionResult.Error!,
                        cancelRetentionResult.Code
                    );
                }
            }
            if (payable is not null)
            {
                payable.Cancel(uid);
            }

            // ── 4. Revertir stock ──────────────────────────────────────────
            // PURCHASE-CANCEL-KARDEX-MOVEMENT-IDENTIFIER-01 — StockMovementType.PurchaseCancelled
            // (nunca PurchaseReturn: esto no es una devolución real a proveedor, es la reversa de
            // stock de una factura anulada). SourceDocType sigue siendo "PurchaseInvoice" — sigue
            // siendo únicamente el documento origen (FACCOM), no el motivo del movimiento.
            // Mismo criterio que Confirm: solo un Product generó PurchaseEntry; un Service con la
            // bodega global de la compra nunca tuvo inventario que revertir.
            var effectiveDate = await _companyClock.TodayAsync(cid, tid, ct);
            var lineItemIds = inv.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value)
                .Distinct().ToList();
            var inventoryItemIds = (await _itemRepo.GetByIdsLightAsync(lineItemIds, tid, ct))
                .Where(i => i.ParticipatesInInventory).Select(i => i.Id).ToHashSet();
            var inventoryLines = inv.Lines
                .Where(l => l.ItemId is { } id && inventoryItemIds.Contains(id)
                    && (l.WarehouseId ?? inv.GlobalWarehouseId) is not null)
                .Select(l => (Line: l, WarehouseId: (l.WarehouseId ?? inv.GlobalWarehouseId)!.Value))
                .ToList();
            // INVENTORY-V1-B4: la reversa conserva el importe de la entrada original de cada línea.
            var entries = MatchPurchaseEntries(inventoryLines,
                (await _stockRepo.GetMovementsByDocumentAsync(tid, inv.Id, "PurchaseInvoice", ct))
                    .Where(m => m.CompanyId == cid && m.MovementType == StockMovementType.PurchaseEntry)
                    .ToList());
            if (entries is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    "No se pudo identificar de forma inequívoca la entrada de inventario de cada línea de la compra para revertirla.");
            }
            foreach (var (line, warehouseId) in inventoryLines)
            {
                await _stockRepo.AppendMovementAsync(
                    tid,
                    cid,
                    line.ItemId!.Value,
                    warehouseId,
                    StockMovementType.PurchaseCancelled,
                    -line.QuantityInBaseUom,
                    line.BaseUomCode,
                    effectiveDate,
                    $"ANULACIÓN: {inv.InvoiceNumber}",
                    inv.Id,
                    "PurchaseInvoice",
                    uid,
                    line.LandedUnitCost,
                    cancellationToken: ct,
                    totalCost: entries[line.Id].TotalCost
                );
            }

            // ── 5. Cambiar estado compra ───────────────────────────────────
            inv.Cancel(cmd.Reason, uid);

            // ── 5b. Liberar la recepción de origen (si la hay) ──────────────
            // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — la recepción que originó esta compra
            // (si vino de una) queda libre para "Crear compra" de nuevo. Se busca por AccessKey (el
            // mismo dato que la vinculó al crearla, ver CreatePurchaseDraftHandler) y se confirma
            // PurchaseId == inv.Id antes de desvincular — nunca desvincula una recepción ajena.
            if (!string.IsNullOrWhiteSpace(inv.AccessKey) && _receptionRepo is not null)
            {
                var reception = await _receptionRepo.GetByAccessKeyAsync(tid, inv.AccessKey, ct);
                if (reception is not null && reception.PurchaseId == inv.Id)
                    reception.UnmarkProcessed(uid);
            }

            // ── 6. Persistir (misma transacción explícita abierta arriba) ──
            // La auditoría de "purchase.cancelled" se registra vía el domain event de inv.Cancel().
            // La retención (si existía) ya quedó anulada en memoria arriba, junto con la reversa de
            // asiento contable (RetentionDocumentCancelledPostingTranslator, disparado por
            // RetentionDocumentCancelledEvent) — todo dentro de este mismo SaveChangesAsync.
            await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);
            await _uow.CommitAsync(ct);

            _logger.LogInformation(
                "Purchase {InvoiceNumber} ({InvoiceId}) cancelled successfully",
                inv.InvoiceNumber,
                inv.Id
            );

            return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
        }
        // ACCOUNTING-CONTINUITY-01: el reverso fallido de un asiento Posted (compra o retención)
        // bloquea la anulación completa — nunca queda anulada con su asiento vigente.
        catch (PurchasePostingFailedException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message, ex.Code);
        }
        catch (RetentionPostingFailedException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message, ex.Code);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// INVENTORY-V1-B4 — entrada PurchaseEntry original de cada línea inventariable. Con
    /// SourceDocLineId se valida contra su detalle; una entrada histórica sin él solo se acepta si
    /// producto, bodega, cantidad base y costo unitario la identifican con una única línea y viceversa.
    /// Faltante, sobrante o ambigüedad ⇒ null (la anulación falla cerrada).
    /// </summary>
    private static Dictionary<Guid, StockMovement>? MatchPurchaseEntries(
        IReadOnlyList<(PurchaseInvoiceDetail Line, Guid WarehouseId)> lines,
        IReadOnlyList<StockMovement> entries)
    {
        static bool Matches((PurchaseInvoiceDetail Line, Guid WarehouseId) l,
            StockMovement m) =>
            m.ProductId == l.Line.ItemId && m.WarehouseId == l.WarehouseId
            && m.Quantity == l.Line.QuantityInBaseUom && m.UnitCost == l.Line.LandedUnitCost;

        if (entries.Count != lines.Count)
            return null;
        var matched = new Dictionary<Guid, StockMovement>();
        foreach (var entry in entries.Where(m => m.SourceDocLineId.HasValue))
        {
            var line = lines.FirstOrDefault(l => l.Line.Id == entry.SourceDocLineId);
            if (line.Line is null || !Matches(line, entry) || !matched.TryAdd(line.Line.Id, entry))
                return null;
        }
        var pendingLines = lines.Where(l => !matched.ContainsKey(l.Line.Id)).ToList();
        var unlinked = entries.Where(m => !m.SourceDocLineId.HasValue).ToList();
        foreach (var line in pendingLines)
        {
            var candidates = unlinked.Where(m => Matches(line, m)).ToList();
            if (candidates.Count != 1 || pendingLines.Count(l => Matches(l, candidates[0])) != 1)
                return null;
            matched.Add(line.Line.Id, candidates[0]);
        }
        return matched;
    }

    private async Task<Result<PurchaseInvoiceDto>> RequestSriAnnulmentAsync(
        PurchaseInvoice inv,
        ERP.Domain.Modules.Retentions.Entities.RetentionDocument retention,
        PurchaseCancellationContext ctx,
        CancellationToken ct
    )
    {
        if (_annulments is null)
            throw new InvalidOperationException(
                "Invariante violada: IRetentionAnnulmentRequester no está registrado."
            );

        var requested = await _annulments.RequestAsync(retention, ctx.Reason, ctx.UserId, ct);
        if (!requested.IsSuccess)
        {
            await _uow.RollbackAsync(ct);
            return Result<PurchaseInvoiceDto>.ValidationFailure(requested.Error!, requested.Code);
        }

        await _uow.SaveChangesAsync(ct);
        await _uow.CommitAsync(ct);
        _logger.LogInformation(
            "Purchase {InvoiceNumber} ({InvoiceId}): SRI annulment requested for its authorized retention {RetentionId}; purchase stays confirmed",
            inv.InvoiceNumber,
            inv.Id,
            retention.Id
        );
        return Result<PurchaseInvoiceDto>.Success(
            PurchaseMapper.ToDto(inv),
            ApiResponseCodes.Retentions.AnnulmentRequested
        );
    }
}

using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 6 — autoriza un <c>PurchaseReturn</c> en <c>Draft</c>: flujo atómico completo
/// (secuencia + inventario + CxP + crédito de proveedor condicional + contabilidad + auditoría),
/// bajo Lock A, idempotente (diseño §16.1/§16.2, fila <c>Authorize</c>).
/// </summary>
internal sealed record AuthorizePurchaseReturnCommand(Guid PurchaseReturnId, Guid ClientRequestId)
    : IRequest<Result<PurchaseReturnDto>>,
        IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

internal sealed class AuthorizePurchaseReturnValidator
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

internal sealed class AuthorizePurchaseReturnHandler
    : IRequestHandler<AuthorizePurchaseReturnCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IRetentionDocumentRepository _retentionRepo;
    private readonly IPurchaseReturnSequenceRepository _sequenceRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICompanyClock _companyClock;
    private readonly IPurchaseCreditNoteRepository? _creditNoteRepo;
    private readonly ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionDocumentRepository? _receptionRepo;
    private readonly ICompanyPrecisionPolicyProvider _precision;

    public AuthorizePurchaseReturnHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IRetentionDocumentRepository retentionRepo,
        IPurchaseReturnSequenceRepository sequenceRepo,
        IStockRepository stockRepo,
        ISupplierCreditRepository creditRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyClock companyClock,
        ICompanyPrecisionPolicyProvider precision,
        IPurchaseCreditNoteRepository? creditNoteRepo = null,
        ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces.IPurchaseReceptionDocumentRepository? receptionRepo =
            null
    )
    {
        _precision = precision;
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _retentionRepo = retentionRepo;
        _sequenceRepo = sequenceRepo;
        _stockRepo = stockRepo;
        _creditRepo = creditRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _b = b;
        _u = u;
        _companyClock = companyClock;
        _creditNoteRepo = creditNoteRepo;
        _receptionRepo = receptionRepo;
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
            if (purchaseReturn is null || purchaseReturn.BranchId != _b.BranchId)
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

            var retention = await _retentionRepo.GetBySourceAsync(
                tid,
                invoice.CompanyId,
                RetentionSourceDocumentType.PurchaseInvoice,
                invoice.Id,
                ct
            );
            var hasIssuedRetention =
                retention is not null && retention.Status == RetentionStatus.Issued;

            // ── PR-004 revalidado bajo lock (§10.2) + snapshot de línea original para Authorize() ──
            var detailIds = purchaseReturn.Lines.Select(l => l.OriginalInvoiceDetailId).ToList();
            var returnedByDetailId = await _returnRepo.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                tid,
                detailIds,
                ct
            );

            var originalLinesByDetailId =
                new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>();
            var quantityDecimals = (await _precision.GetEffectiveAsync(ct)).QuantityDecimals;
            var baseQuantityByLineId = new Dictionary<Guid, (decimal Quantity, string UomCode)>();
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

                // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-1) — fuente de verdad es
                // originalLine.Taxes (PurchaseInvoiceDetailTax), nunca los campos escalares legacy
                // (VatAmount/IceAmount son un legacy compatibility mirror). VAT/ICE se derivan de la
                // misma colección para no duplicar la fuente.
                var originalTaxes = originalLine
                    .Taxes.Select(t => new PurchaseReturn.OriginalLineTaxSnapshot(
                        t.TaxCode,
                        t.TaxRateCode,
                        t.TaxName,
                        t.Rate,
                        t.CalculationType,
                        t.TaxableBase,
                        t.TaxAmount
                    ))
                    .ToList();
                var originalVat = originalTaxes.FirstOrDefault(t =>
                    t.TaxCode == SriTaxCategoryCodes.Vat
                );
                if (originalVat is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        $"La línea '{originalLine.Description}' de la factura original no tiene "
                            + "un impuesto IVA registrado — no se puede autorizar la devolución."
                    );
                }
                var originalIce = originalTaxes.FirstOrDefault(t =>
                    t.TaxCode == SriTaxCategoryCodes.Ice
                );

                originalLinesByDetailId[originalLine.Id] = new PurchaseReturn.OriginalLineSnapshot(
                    originalLine.Quantity,
                    originalLine.LineSubtotal,
                    originalLine.DiscountAmount,
                    originalVat.TaxAmount,
                    originalIce?.TaxAmount ?? 0m,
                    originalVat.TaxRateCode,
                    originalVat.Rate ?? 0m,
                    originalIce?.TaxRateCode,
                    originalIce?.Rate ?? 0m,
                    originalTaxes
                );
                // PURCHASE-RETURN-COST-FIX-01: la devolución se expresa en la unidad comercial de la
                // factura; stock/Kardex siempre en unidad base con el factor de conversión original
                // (mismo criterio que ConfirmPurchase/CancelPurchase).
                baseQuantityByLineId[line.Id] = (
                    line.Quantity == originalLine.Quantity
                        ? originalLine.QuantityInBaseUom
                        : Math.Round(
                            line.Quantity * originalLine.ConversionFactor,
                            quantityDecimals,
                            MidpointRounding.AwayFromZero
                        ),
                    originalLine.BaseUomCode
                );
            }

            // ── PR-005: stock suficiente en la bodega original de cada línea (§14.2) — chequeo
            // preventivo antes de consumir un número de secuencia; CurrentStock.ApplyMovement
            // (dentro de AppendMovementAsync, más abajo) es la defensa autoritativa final. ──
            foreach (var line in purchaseReturn.Lines)
            {
                var stock = await _stockRepo.GetStockAsync(tid, line.WarehouseId, line.ItemId, ct);
                var availableQty = stock?.Quantity ?? 0m;
                var requiredBaseQty = baseQuantityByLineId[line.Id].Quantity;
                if (availableQty < requiredBaseQty)
                {
                    await _uow.RollbackAsync(ct);
                    // PR-005
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        $"Stock insuficiente en la bodega original para la línea (existencia: {availableQty}, "
                            + $"solicitado: {requiredBaseQty})."
                    );
                }
            }

            var creditNote = _creditNoteRepo is null
                ? null
                : await _creditNoteRepo.GetByLinkedPurchaseReturnIdAsync(
                    tid,
                    purchaseReturn.Id,
                    ct
                );
            ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument? reception =
                null;
            if (creditNote is not null)
            {
                if (creditNote.Status != PurchaseCreditNoteStatus.Draft)
                    throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                        "La nota de crédito vinculada no está en borrador."
                    );
                var resolved = await CreditNoteReturnLines.ResolveAsync(
                    invoice,
                    purchaseReturn
                        .Lines.Select(l => new PurchaseReturnDraftLineInput(
                            l.OriginalInvoiceDetailId,
                            l.Quantity
                        ))
                        .ToList(),
                    _returnRepo,
                    tid,
                    quantityDecimals,
                    ct
                );
                if (resolved.Error is not null)
                    throw new ERP.Domain.Exceptions.DomainRuleViolationException(resolved.Error);
                var total = resolved.Fiscal.Sum(l =>
                    l.Subtotal + l.VatAmount + l.IceAmount + l.IrbpnrAmount
                );
                if (
                    total != creditNote.TotalAmount
                    || creditNote.Lines.Count != purchaseReturn.Lines.Count
                    || creditNote.Lines.Any(l =>
                        !purchaseReturn.Lines.Any(r =>
                            r.OriginalInvoiceDetailId == l.PurchaseInvoiceDetailId
                            && r.Quantity == l.Quantity
                        )
                    )
                )
                    throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                        "Las líneas o el total de la devolución no coinciden con la NC vinculada."
                    );
                if (creditNote.ReceptionDocumentId is { } receptionId)
                {
                    reception = await _receptionRepo!.GetByIdAsync(tid, receptionId, ct);
                    if (reception is null || Math.Abs(total - reception.TotalAmount) > 0.01m)
                        throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                            "El total de la devolución no coincide con la NC/XML recibido."
                        );
                    if (
                        reception.CompanyId != creditNote.CompanyId
                        || reception.SourceDocType
                            != Domain
                                .Modules
                                .Purchases
                                .PurchaseReception
                                .Enums
                                .PurchaseReceptionSourceDocType
                                .CreditNote
                        || reception.Status
                            != Domain
                                .Modules
                                .Purchases
                                .PurchaseReception
                                .Enums
                                .PurchaseReceptionDocumentStatus
                                .Verified
                        || reception.PurchaseId is not null
                        || (
                            reception.SupplierId is { } supplierId
                            && supplierId != invoice.SupplierId
                        )
                        || reception.AccessKey != creditNote.AccessKey
                        || (
                            !string.IsNullOrWhiteSpace(reception.ModifiedDocumentNumber)
                            && !string.Equals(
                                reception.ModifiedDocumentNumber.Trim(),
                                invoice.InvoiceNumber.Trim(),
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    )
                        throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                            "La recepción de la NC ya no es válida para esta factura."
                        );
                }
            }

            var returnNumber = await _sequenceRepo.CaptureNextAsync(
                tid,
                purchaseReturn.CompanyId,
                ct
            );
            var authorizeHash = ComputeAuthorizePayloadHash(purchaseReturn.Id, cmd.ClientRequestId);
            var balanceDueBeforeApplication = payable.OutstandingAmount;

            // PURCHASE-RETURN-COST-FIX-01 (ADR-041 addendum): la salida de Kardex ocurre ANTES de
            // Authorize() y en unidad base, valorada al promedio ponderado vigente (unitCost null —
            // nunca LandedUnitCost). Su valor real alimenta HistoricalCost/UnitCost de la línea; la NC
            // comercial/fiscal sigue derivando exclusivamente de la factura original.
            var effectiveDate = await _companyClock.TodayAsync(purchaseReturn.CompanyId, tid, ct);
            var inventoryExitByLineId =
                new Dictionary<Guid, PurchaseReturn.InventoryExitValuation>();
            foreach (var line in purchaseReturn.Lines)
            {
                var (baseQuantity, baseUomCode) = baseQuantityByLineId[line.Id];
                var movement = await _stockRepo.AppendMovementAsync(
                    tid,
                    purchaseReturn.CompanyId,
                    line.ItemId,
                    line.WarehouseId,
                    StockMovementType.PurchaseReturn,
                    -baseQuantity,
                    baseUomCode,
                    effectiveDate,
                    $"Devolución {returnNumber.Trim()}",
                    purchaseReturn.Id,
                    "PurchaseReturn",
                    uid,
                    sourceDocLineId: line.Id,
                    cancellationToken: ct
                );
                if (movement.TotalCost is not { } exitCost)
                    throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                        "El producto no tiene un costo de inventario valorado para registrar la devolución."
                    );
                // Costo unitario = promedio consumido por la salida; el importe es el canónico del
                // Kardex (INVENTORY-V1-B3: incluye el residuo si la devolución vacía la bodega).
                inventoryExitByLineId[line.Id] = new PurchaseReturn.InventoryExitValuation(
                    movement.CostBasis ?? exitCost / baseQuantity,
                    exitCost
                );
            }

            SupplierCredit? credit;
            credit = purchaseReturn.Authorize(
                returnNumber,
                originalLinesByDetailId,
                balanceDueBeforeApplication,
                invoice.CurrencyCode,
                hasIssuedRetention,
                uid,
                cmd.ClientRequestId,
                authorizeHash,
                inventoryExitByLineId
            );

            if (creditNote is not null)
            {
                creditNote.CompleteLinkedReturn(purchaseReturn, uid, cmd.ClientRequestId,
                    AuthorizePurchaseCreditNoteHandler.ComputeAuthorizePayloadHash(creditNote.Id, cmd.ClientRequestId));
                purchaseReturn.RegisterLinkedCreditNote(creditNote, uid);
                reception?.MarkProcessed(invoice.Id, uid);
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

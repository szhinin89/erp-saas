using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases;
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
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseReturnSequenceRepository _sequenceRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly IPostingEngine _postingEngine;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public AuthorizePurchaseReturnHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReturnSequenceRepository sequenceRepo,
        IStockRepository stockRepo,
        ISupplierCreditRepository creditRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        IPostingEngine postingEngine,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _sequenceRepo = sequenceRepo;
        _stockRepo = stockRepo;
        _creditRepo = creditRepo;
        _uow = uow;
        _dbEx = dbEx;
        _postingEngine = postingEngine;
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
                var originalVat = originalTaxes.FirstOrDefault(t => t.TaxCode == SriTaxCategoryCodes.Vat);
                if (originalVat is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        $"La línea '{originalLine.Description}' de la factura original no tiene "
                            + "un impuesto IVA registrado — no se puede autorizar la devolución."
                    );
                }
                var originalIce = originalTaxes.FirstOrDefault(t => t.TaxCode == SriTaxCategoryCodes.Ice);

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
                    originalLine.LandedUnitCost,
                    originalTaxes
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

            // ── Guard IRBPNR (TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E) — mismo criterio que
            // ConfirmPurchaseUseCases STEP 0: el posting nunca revierte una autorización ya
            // persistida (un Result fallido de IPostingEngine.PostAsync solo se registra en log),
            // así que la única forma confiable de exigir configuración contable es esta
            // precondición, antes de consumir el secuencial/Authorize(). ──
            if (
                originalLinesByDetailId.Values.Any(s =>
                    s.Taxes.Any(t => t.TaxCode == SriTaxCategoryCodes.Irbpnr && t.TaxAmount > 0)
                )
            )
            {
                var irbpnrConfigured = await _postingEngine.IsAmountKindConfiguredAsync(
                    tid,
                    purchaseReturn.CompanyId,
                    "Purchases",
                    "PurchaseReturn",
                    PostingAmountKind.TaxIrbpnr,
                    ct
                );
                if (!irbpnrConfigured)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseReturnDto>.ValidationFailure(
                        "Esta devolución contiene IRBPNR (impuesto código 5), pero no existe una regla de "
                            + "contabilización (PostingRuleLine) configurada para IRBPNR en Devoluciones de Compra. "
                            + "Configure la cuenta contable correspondiente antes de autorizar."
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

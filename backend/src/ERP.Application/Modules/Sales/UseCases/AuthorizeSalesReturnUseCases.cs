using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Sales.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

/// <summary>
/// Asignación de reembolso elegida explícitamente por el operador (P0-01, decisión de producto
/// 2 — sin prorrateo automático entre formas de pago). <see cref="Method"/> es el nombre de
/// <see cref="SalesReturnRefundMethod"/> ("Cash"/"ReceivableCredit").
/// </summary>
public sealed record AuthorizeSalesReturnRefundAllocationInput(string Method, decimal Amount);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// Autoriza una devolución en <c>Draft</c>: congela líneas, valida
/// <c>Σ RefundAllocation.Amount == GrandTotal</c> (invariante de dominio, fuente de verdad — no
/// se duplica aquí) y revierte el inventario correspondiente. No implementa todavía efectos de
/// Caja/CxC/Contabilidad/Documento Electrónico (fases posteriores).
/// </summary>
public sealed record AuthorizeSalesReturnCommand(
    Guid SalesReturnId,
    List<AuthorizeSalesReturnRefundAllocationInput> RefundAllocations
) : IRequest<Result<SalesReturnDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class AuthorizeSalesReturnCommandValidator
    : AbstractValidator<AuthorizeSalesReturnCommand>
{
    public AuthorizeSalesReturnCommandValidator()
    {
        RuleFor(x => x.SalesReturnId).NotEmpty();
        RuleFor(x => x.RefundAllocations)
            .NotEmpty()
            .WithMessage("Debe indicar al menos una asignación de reembolso.");
        RuleForEach(x => x.RefundAllocations)
            .ChildRules(allocation =>
            {
                allocation
                    .RuleFor(a => a.Amount)
                    .GreaterThan(0)
                    .WithMessage("El monto de la asignación de reembolso debe ser mayor a cero.");
                allocation
                    .RuleFor(a => a.Method)
                    .NotEmpty()
                    .WithMessage("La forma de reembolso es obligatoria.")
                    .Must(m => Enum.TryParse<SalesReturnRefundMethod>(m, true, out _))
                    .WithMessage("La forma de reembolso indicada no es válida.");
            });
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class AuthorizeSalesReturnHandler
    : IRequestHandler<AuthorizeSalesReturnCommand, Result<SalesReturnDto>>
{
    private readonly ISalesReturnRepository _returnRepo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly IStockRepository _stockRepo;
    private readonly SalesReturnRefundHandler _refundHandler;
    private readonly IDocumentSequenceRepository _seqRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly IEstablishmentRepository _estRepo;
    private readonly IElectronicDocumentIssuer _edocIssuer;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;
    private readonly ILogger<AuthorizeSalesReturnHandler> _logger;

    public AuthorizeSalesReturnHandler(
        ISalesReturnRepository returnRepo,
        ISalesInvoiceRepository invoiceRepo,
        IStockRepository stockRepo,
        SalesReturnRefundHandler refundHandler,
        IDocumentSequenceRepository seqRepo,
        IEmissionPointRepository epRepo,
        IEstablishmentRepository estRepo,
        IElectronicDocumentIssuer edocIssuer,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u,
        ILogger<AuthorizeSalesReturnHandler> logger
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _stockRepo = stockRepo;
        _refundHandler = refundHandler;
        _seqRepo = seqRepo;
        _epRepo = epRepo;
        _estRepo = estRepo;
        _edocIssuer = edocIssuer;
        _uow = uow;
        _t = t;
        _c = c;
        _u = u;
        _logger = logger;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        AuthorizeSalesReturnCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;
        var uid = _u.UserId;

        // 1-2. Cargar la devolución y verificar existencia.
        var salesReturn = await _returnRepo.GetByIdAsync(tid, cmd.SalesReturnId, ct);
        if (salesReturn is null)
            return Result<SalesReturnDto>.NotFound("Devolución no encontrada.");

        // La transacción se abre explícitamente ANTES del advisory lock (Fase 5.5.1, mismo
        // hallazgo documentado en PostingIdempotencyGuard): sin ella,
        // AcquireReturnLockAsync corre en su propio statement autocommit que se libera de
        // inmediato y el lock deja de serializar nada. ErpDbContext.SaveChangesAsync detecta la
        // transacción ambiente más abajo (Database.CurrentTransaction) y no intenta abrir/comitear
        // una propia — el commit/rollback queda enteramente a cargo de este handler.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            // 3. Advisory lock por factura (Fase 3) — serializa toda autorización concurrente
            // sobre la misma factura ANTES de revalidar remanente, cerrando la ventana de
            // condición de carrera que la validación de la Fase 4 (Draft) no podía cerrar por sí
            // sola.
            await _returnRepo.AcquireReturnLockAsync(tid, salesReturn.SalesInvoiceId, ct);

            // 4. Revalidar remanente bajo lock — la validación al crear/editar el Draft (Fase 4)
            // es solo preventiva (UX temprana); esta es la que garantiza consistencia real.
            var invoice = await _invoiceRepo.GetByIdAsync(tid, salesReturn.SalesInvoiceId, ct);
            if (invoice is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SalesReturnDto>.NotFound("Factura no encontrada.");
            }

            foreach (var line in salesReturn.Lines)
            {
                var originalLine = invoice.Lines.FirstOrDefault(l =>
                    l.Id == line.OriginalInvoiceDetailId
                );
                if (originalLine is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SalesReturnDto>.ValidationFailure(
                        "La línea de factura original ya no existe."
                    );
                }

                var remainingError = await SalesReturnRemainingQuantityGuard.ValidateAsync(
                    _returnRepo,
                    tid,
                    originalLine,
                    line.Quantity,
                    ct
                );
                if (remainingError is not null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SalesReturnDto>.ValidationFailure(remainingError);
                }
            }

            // 5. Asignaciones de reembolso + Authorize — el dominio es la única fuente de verdad
            // de Σ RefundAllocation.Amount == GrandTotal (no se revalida aquí).
            foreach (var input in cmd.RefundAllocations)
            {
                if (!Enum.TryParse<SalesReturnRefundMethod>(input.Method, true, out var method))
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SalesReturnDto>.ValidationFailure(
                        $"Forma de reembolso inválida: '{input.Method}'."
                    );
                }
                salesReturn.AddRefundAllocation(
                    SalesReturnRefundAllocation.Create(salesReturn.Id, tid, method, input.Amount),
                    uid
                );
            }

            salesReturn.Authorize(uid);

            // 6. Reversión de inventario (Kardex) — documento origen es la propia devolución,
            // nunca la factura (una factura puede tener múltiples devoluciones parciales en el
            // tiempo). SALES-PRESENTATIONS-02: reingresa QuantityInBaseUom/BaseUomCode — la
            // devolución siempre hereda la presentación de la línea de venta original, nunca
            // Quantity/UomCode crudos.
            foreach (var line in salesReturn.Lines)
            {
                if (line.ItemId is null || line.WarehouseId is null)
                    continue;

                await _stockRepo.AppendMovementAsync(
                    tid,
                    cid,
                    line.ItemId.Value,
                    line.WarehouseId.Value,
                    StockMovementType.SaleReturn,
                    line.QuantityInBaseUom,
                    line.BaseUomCode,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    $"DEV-{salesReturn.ReturnNumber}",
                    salesReturn.Id,
                    "SalesReturn",
                    uid,
                    cancellationToken: ct
                );
            }

            // 7. Ejecutar el reembolso (Fase 6) — completamente síncrono, dentro de la misma
            // transacción: si falla Caja o CxC, toda la autorización se revierte. Nunca un
            // INotificationHandler/evento asíncrono (decisión de arquitectura de esta fase).
            var refundError = await _refundHandler.ExecuteAsync(salesReturn, tid, uid, ct);
            if (refundError is not null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SalesReturnDto>.ValidationFailure(refundError);
            }

            // 8. Capturar el secuencial SRI "04" (Nota de Crédito) — mismo punto de emisión que
            // la factura original (SalesReturn no tiene uno propio). Se congela una única vez en
            // el propio agregado (SalesReturn.SetCreditNoteDocumentNumber), mismo criterio exacto
            // que SalesInvoice.SetInvoiceNumber: nunca se vuelve a solicitar en reintentos.
            string? creditNoteNumber = null;
            if (invoice.EmissionPointId is { } emissionPointId)
            {
                var ep = await _epRepo.GetByIdAsync(emissionPointId, tid, ct);
                var est = ep is null
                    ? null
                    : await _estRepo.GetByIdAsync(tid, ep.EstablishmentId, ct);
                if (ep is not null && est is not null)
                {
                    var sequential = await _seqRepo.CaptureNextAsync(
                        tid,
                        cid,
                        emissionPointId,
                        SalesReturnCreditNoteDataProvider.CreditNoteDocTypeCode,
                        ct
                    );
                    creditNoteNumber = $"{est.Code}-{ep.Code}-{sequential}";
                    salesReturn.SetCreditNoteDocumentNumber(creditNoteNumber);
                }
                else
                {
                    _logger.LogWarning(
                        "SalesReturn {SalesReturnId}: no se pudo resolver el punto de emisión/establecimiento de la factura original — la Nota de Crédito electrónica no se emitirá.",
                        salesReturn.Id
                    );
                }
            }
            else
            {
                _logger.LogWarning(
                    "SalesReturn {SalesReturnId}: la factura original no tiene punto de emisión asignado — la Nota de Crédito electrónica no se emitirá.",
                    salesReturn.Id
                );
            }

            // 9. Persistir todo en la misma unidad de trabajo/transacción.
            await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);
            await _uow.CommitAsync(ct);

            // 10. Emisión de la Nota de Crédito electrónica — SIEMPRE después del commit, nunca
            // antes (mismo criterio que AuthorizeSalesUseCases.cs para Factura): el pipeline hace
            // E/S externa real (SOAP al SRI) que no debe ejecutarse con la transacción/el
            // advisory lock de la devolución todavía abiertos. Un fallo aquí es informativo — la
            // devolución ya quedó autorizada y comprometida; el documento SRI puede reintentarse
            // vía el job genérico de reintentos sin volver a autorizar nada.
            if (creditNoteNumber is not null)
            {
                var registerResult = await _edocIssuer.RegisterAsync(
                    new RegisterElectronicDocumentRequest(
                        tid,
                        cid,
                        ElectronicDocumentType.CreditNote,
                        "Sales",
                        salesReturn.Id,
                        uid
                    ),
                    ct
                );
                if (!registerResult.IsSuccess)
                    _logger.LogWarning(
                        "Emisión de Nota de Crédito falló para SalesReturn {SalesReturnId} ({CreditNoteNumber}): {Code} — {Error}",
                        salesReturn.Id,
                        creditNoteNumber,
                        registerResult.Code,
                        registerResult.Error
                    );
            }

            return Result<SalesReturnDto>.Success(SalesReturnMapper.ToDto(salesReturn));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SalesReturnDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}

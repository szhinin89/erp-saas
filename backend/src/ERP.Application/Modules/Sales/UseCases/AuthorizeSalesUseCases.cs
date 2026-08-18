using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.Policies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Sales.UseCases;

/// <summary>
/// El cliente nunca envía EmissionPointId (ADR — Rediseño del módulo de Caja, Fase 4): la
/// autorización siempre usa el EmissionPointId ya fijado en la factura al crear el borrador
/// (resuelto entonces desde ICurrentCashSession) — nunca un override manual.
/// </summary>
public sealed record AuthorizeSalesInvoiceCommand(Guid InvoiceId)
    : IRequest<Result<SalesInvoiceDto>>,
        IBranchScopedRequest;

public sealed class AuthorizeSalesInvoiceHandler
    : IRequestHandler<AuthorizeSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    // SRI reporta la tolerancia como "129600 minutos" en el mensaje de rechazo [65]
    // FECHA EMISIÓN EXTEMPORANEA (129600 / 60 / 24 = 90 días). Validado con evidencia real
    // (rechazos capturados en electronic_document_sri_message) antes de esta corrección.
    private const int SriIssueDateToleranceDays = 90;

    private readonly ISalesInvoiceRepository _repo;
    private readonly ISalesReceivableRepository _rxRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IDocumentSequenceRepository _seqRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly IEstablishmentRepository _estRepo;
    private readonly ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository _edocRepo;
    private readonly ISalesInvoiceEmissionStrategyResolver _emissionStrategyResolver;
    private readonly ERP.Application.Common.Services.ICompanyClock _companyClock;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ISalesFiscalPolicyResolver _fiscalPolicyResolver;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly ILogger<AuthorizeSalesInvoiceHandler> _logger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public AuthorizeSalesInvoiceHandler(
        ISalesInvoiceRepository repo,
        ISalesReceivableRepository rxRepo,
        IStockRepository stockRepo,
        ISriTaxResolver tax,
        IDocumentSequenceRepository seqRepo,
        IEmissionPointRepository epRepo,
        IEstablishmentRepository estRepo,
        ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository edocRepo,
        ISalesInvoiceEmissionStrategyResolver emissionStrategyResolver,
        ERP.Application.Common.Services.ICompanyClock companyClock,
        IBusinessPartnerRepository bpRepo,
        ISalesFiscalPolicyResolver fiscalPolicyResolver,
        IPaymentMethodRepository paymentMethodRepo,
        ILogger<AuthorizeSalesInvoiceHandler> logger,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _rxRepo = rxRepo;
        _stockRepo = stockRepo;
        _tax = tax;
        _seqRepo = seqRepo;
        _epRepo = epRepo;
        _estRepo = estRepo;
        _edocRepo = edocRepo;
        _emissionStrategyResolver = emissionStrategyResolver;
        _companyClock = companyClock;
        _bpRepo = bpRepo;
        _fiscalPolicyResolver = fiscalPolicyResolver;
        _paymentMethodRepo = paymentMethodRepo;
        _logger = logger;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        AuthorizeSalesInvoiceCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;
        var uid = _u.UserId;

        var inv = await _repo.GetByIdAsync(tid, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        if (inv.Status != Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft)
            return Result<SalesInvoiceDto>.ValidationFailure("Esta factura ya fue autorizada.");

        // ── Validar fecha de emisión contra la fecha empresarial (Ecuador) ──
        // Corrige SRI [65] FECHA EMISIÓN EXTEMPORÁNEA: debe ejecutarse ANTES de capturar
        // secuencial y generar el XML — evita consumir un número de factura y enviar al SRI
        // un comprobante que será rechazado. Usa ICompanyClock (zona horaria de la empresa),
        // nunca DateTime.UtcNow.Date, para no repetir el desfase que causó el bug original.
        var companyToday = await _companyClock.TodayAsync(cid, tid, ct);
        if (inv.IssueDate > companyToday)
            return Result<SalesInvoiceDto>.ValidationFailure(
                $"La fecha de emisión ({inv.IssueDate:dd/MM/yyyy}) no puede ser posterior a la fecha actual ({companyToday:dd/MM/yyyy})."
            );
        if (inv.IssueDate < companyToday.AddDays(-SriIssueDateToleranceDays))
            return Result<SalesInvoiceDto>.ValidationFailure(
                $"No puedes emitir esta factura porque la fecha de emisión ({inv.IssueDate:dd/MM/yyyy}) es demasiado antigua "
                    + $"(supera el máximo de {SriIssueDateToleranceDays} días permitido). Corrige la fecha de emisión o contacta a "
                    + "soporte si necesitas registrar una venta retroactiva."
            );

        // ── Recalcular impuestos con nombres actualizados ───────────
        foreach (var line in inv.Lines)
        {
            var vatResult = await _tax.GetVatRateWithNameAsync(line.VatCode, ct);
            if (vatResult is null)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"Código IVA '{line.VatCode}' no encontrado."
                );

            decimal iceRate = 0;
            string? iceName = null;
            if (!string.IsNullOrWhiteSpace(line.IceCode))
            {
                var iceResult = await _tax.GetIceRateWithNameAsync(line.IceCode, ct);
                if (iceResult is null)
                    return Result<SalesInvoiceDto>.ValidationFailure(
                        $"Código ICE '{line.IceCode}' no encontrado."
                    );
                iceRate = iceResult.Rate;
                iceName = iceResult.Name;
            }
            line.ApplyTaxes(
                line.VatCode,
                vatResult.Rate,
                vatResult.Name,
                line.IceCode,
                iceRate,
                iceName
            );
        }

        // ── Modalidad de pago real (BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01) ─────
        // Único punto de estas dos señales — nunca recalculadas por separado en otra validación.
        // isCredit (alias de IsCreditByTerm) se conserva con este nombre porque el bloque de
        // generación de CxC más abajo lo reutiliza — esa condición de negocio (cuándo se genera
        // CxC) no cambia con este fix.
        var isCredit = inv.CreditTermDays > 0 || inv.PaymentTerm.Installments > 1;

        var isCreditByPaymentMethod = false;
        foreach (var payment in inv.Payments)
        {
            var method = await _paymentMethodRepo.GetByIdAsync(tid, payment.PaymentMethodId, ct);
            if (method?.IsCreditAllowed == true)
            {
                isCreditByPaymentMethod = true;
                break;
            }
        }

        var paymentModality = new SalesPaymentModality(isCredit, isCreditByPaymentMethod);

        // ── Validar política fiscal de Consumidor Final (autoridad backend) ─
        // Único punto de esta regla — nunca duplicada en otro handler ni confiada al frontend.
        // Se evalúa ANTES que la consistencia general: el mensaje fiscal de Consumidor Final
        // tiene prioridad sobre el mensaje genérico de consistencia (regla fiscal más fuerte).
        var customer = await _bpRepo.GetByIdAsync(inv.CustomerId, ct);
        if (customer is not null && customer.Identification.IsConsumidorFinal())
        {
            var policy = await _fiscalPolicyResolver.GetEffectivePolicyAsync(ct);

            if (paymentModality.IsCreditSale && policy.BlockConsumerFinalCredit)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    SalesFiscalPolicyMessages.CreditBlockedMessage
                );

            if (inv.GrandTotal > policy.ConsumerFinalMaxAmount)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    SalesFiscalPolicyMessages.AmountExceededMessage(policy.ConsumerFinalMaxAmount)
                );
        }

        // ── Validar consistencia condición de pago ↔ método de pago (cualquier cliente) ─
        // Caso real que motivó esta regla: factura 001-001-000000016, PaymentTerm Contado pagada
        // con método Crédito — quedó autorizada sin generar CxC. Venta mixta (parte contado +
        // parte crédito) queda fuera de alcance de esta fase.
        if (!paymentModality.IsConsistent)
        {
            var message = paymentModality.IsCreditByTerm
                ? SalesPaymentConsistencyMessages.TermCreditMethodCashOnlyMessage
                : SalesPaymentConsistencyMessages.TermCashMethodCreditMessage;
            return Result<SalesInvoiceDto>.ValidationFailure(message);
        }

        // ── Validar stock disponible por línea (Kardex) ─────────────
        // "Sin stock suficiente, no se factura" — decisión de negocio explícita.
        // WarehouseId solo está poblado en líneas cuyo ítem controla inventario
        // (impuesto por SalesLineBuilder al crear el borrador).
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null || line.WarehouseId is null)
                continue;

            var stock = await _stockRepo.GetStockAsync(
                tid,
                line.WarehouseId.Value,
                line.ItemId.Value,
                ct
            );
            var available = stock?.Quantity ?? 0m;
            if (available < line.Quantity)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"Línea '{line.Description}': stock insuficiente en la bodega seleccionada "
                        + $"(disponible: {available}, solicitado: {line.Quantity}). "
                        + "Reduce la cantidad, elige otra bodega, o realiza un ingreso de inventario antes de emitir."
                );
        }

        // ── Generar número secuencial SRI ───────────────────────────
        // EmissionPointId nunca se sobreescribe aquí: ya quedó fijado al crear el borrador
        // (resuelto entonces desde ICurrentCashSession) — la autorización solo lo consume.
        //
        // Fuente de verdad del tipo de emisión: se lee de EmissionPoint.EmissionType, recién
        // cargado aquí — nunca de inv.EmissionType (snapshot fijado al crear el borrador). Se
        // captura en esta variable hoisted para que el bloque de emisión (más abajo) resuelva
        // la estrategia correspondiente sin volver a decidir nada por su cuenta.
        var epId = inv.EmissionPointId;
        EmissionType? emissionPointType = null;
        if (epId.HasValue)
        {
            var ep = await _epRepo.GetByIdAsync(epId.Value, tid, ct);
            if (ep is null)
                return Result<SalesInvoiceDto>.ValidationFailure("Punto de emisión no encontrado.");

            var est = await _estRepo.GetByIdAsync(tid, ep.EstablishmentId, ct);
            if (est is null)
                return Result<SalesInvoiceDto>.ValidationFailure("Establecimiento no encontrado.");

            // CaptureNextAsync: atómico (advisory lock + transacción propia).
            // Usa inv.DocTypeCode para soportar facturas, NC, ND, etc. sin hardcode.
            // Común a Electronic y Physical — ambos consumen secuencial SRI por igual.
            var sequential = await _seqRepo.CaptureNextAsync(
                tid,
                cid,
                epId.Value,
                inv.DocTypeCode,
                ct
            );
            inv.SetInvoiceNumber($"{est.Code}-{ep.Code}-{sequential}");

            emissionPointType = ep.EmissionType;
        }

        // ── Autorizar (congela líneas + snapshot totales) ───────────
        try
        {
            inv.Authorize(uid);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Authorize rejected for sales invoice {InvoiceId} tenant {TenantId}: {Reason}",
                cmd.InvoiceId,
                tid,
                ex.Message
            );
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        // ── Egreso de inventario (Kardex) — solo consume lo ya confirmado en el
        // documento; precio/descuento/IVA siguen siendo propiedad exclusiva de Ventas ──
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null || line.WarehouseId is null)
                continue;

            await _stockRepo.AppendMovementAsync(
                tid,
                cid,
                line.ItemId.Value,
                line.WarehouseId.Value,
                StockMovementType.SaleExit,
                -line.Quantity,
                line.UomCode,
                inv.IssueDate,
                inv.InvoiceNumber,
                inv.Id,
                "SalesInvoice",
                uid,
                cancellationToken: ct
            );
        }

        // ── Generar cuenta por cobrar (solo crédito — contado no genera CxC) ─
        // isCredit ya calculado arriba (validación de política fiscal de Consumidor Final).
        if (isCredit && inv.GrandTotal > 0)
        {
            var receivable = Domain.Modules.Sales.Entities.SalesReceivable.Create(
                tid,
                cid,
                inv.Id,
                inv.CustomerId,
                inv.GrandTotal,
                uid
            );
            receivable.GenerateInstallments(
                inv.IssueDate,
                inv.CreditTermDays,
                inv.PaymentTerm.Installments
            );
            await _rxRepo.AddAsync(receivable, ct);
        }

        _logger.LogInformation(
            "Authorizing sales invoice {InvoiceNumber} ({InvoiceId}) for tenant {TenantId}. Lines: {LineCount}, Total: {GrandTotal}",
            inv.InvoiceNumber,
            inv.Id,
            tid,
            inv.Lines.Count,
            inv.GrandTotal
        );

        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        _logger.LogInformation(
            "Sales invoice {InvoiceNumber} ({InvoiceId}) authorized successfully",
            inv.InvoiceNumber,
            inv.Id
        );

        // Fase 10 (integración final): dispara el flujo posterior a la autorización SOLO después
        // de que la transacción de Ventas ya fue confirmada (SaveChangesWithSequenceRetryAsync
        // arriba, con su propio commit). Deliberadamente NO se dispara desde
        // SalesInvoiceAuthorizedEvent (domain event, ver ErpDbContext.SaveChangesAsync): ese
        // evento se despacha de forma síncrona ANTES del commit de la transacción de Ventas, y la
        // estrategia electrónica hace E/S externa real (SOAP al SRI, con hasta ~30s de espera en
        // la consulta de autorización) — ejecutarlo ahí mantendría abierta la transacción/los
        // locks de la factura durante toda esa espera, acoplando la autorización comercial a la
        // disponibilidad del SRI. Disparar aquí, después del commit, evita ese riesgo sin
        // necesidad de Hangfire/BackgroundService/Task.Run.
        //
        // Único punto de disparo del flujo posterior a la autorización para Ventas — condiciones
        // exigidas: factura autorizada internamente (garantizado, este código solo se alcanza tras
        // inv.Authorize() exitoso) y con secuencial definitivo (emissionPointType solo tiene valor
        // cuando hubo EmissionPointId y se capturó secuencial SRI). Este handler únicamente
        // resuelve el tipo de emisión (ya resuelto arriba, desde EmissionPoint.EmissionType) y
        // delega en la estrategia correspondiente — nunca vuelve a decidir aquí si algo es
        // electrónico o físico. La completitud de la información específica del comprobante
        // electrónico la valida el propio proveedor (SalesInvoiceElectronicDocumentDataProvider),
        // no se duplica esa validación aquí. electronicIssueError viaja únicamente en la respuesta
        // de ESTA request — informativo para el usuario que autorizó, nunca reinterpretado como
        // fallo del comando (la autorización comercial ya está confirmada e irreversible en este
        // punto).
        string? electronicIssueError = null;
        if (emissionPointType.HasValue)
        {
            var strategy = _emissionStrategyResolver.Resolve(emissionPointType.Value);
            electronicIssueError = await strategy.ExecuteAsync(
                new SalesInvoiceEmissionContext(tid, cid, uid, inv),
                ct
            );
        }

        var edoc = await _edocRepo.GetBySourceAsync(tid, "Sales", inv.Id, ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv, edoc, electronicIssueError));
    }
}

using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record SalesLineInput(
    Guid? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    string? Notes = null,
    decimal DiscountPct = 0,
    string? IceCode = null,
    Guid? WarehouseId = null
);

// ── Commands & Queries ──────────────────────────────────────────────────

public sealed record SalesPaymentInput(
    Guid PaymentMethodId,
    decimal Amount,
    string? Reference = null,
    CardDetailInput? CardDetail = null,
    TransferDetailInput? TransferDetail = null,
    ChequeDetailInput? ChequeDetail = null
);

public sealed record CardDetailInput(
    string? CardBrand = null,
    string? CardLastFour = null,
    string? BankName = null,
    string? AuthorizationCode = null,
    string? LotNumber = null
);

public sealed record TransferDetailInput(
    string? BankName = null,
    string? ReceiptNumber = null,
    string? TransferDate = null
);

public sealed record ChequeDetailInput(
    string? BankName = null,
    string? ChequeNumber = null,
    string? HolderName = null,
    string? CashDate = null
);

/// <summary>
/// El cliente nunca envía EmissionPointId ni CashSessionId (ADR — Rediseño del módulo de Caja,
/// Fase 4): el servidor los resuelve exclusivamente desde <c>ICurrentCashSession</c>. Si el
/// usuario no tiene una caja abierta, la creación del borrador se rechaza.
/// </summary>
public sealed record CreateSalesDraftCommand(
    Guid CustomerId,
    DateOnly IssueDate,
    List<SalesLineInput> Lines,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    List<SalesPaymentInput>? Payments = null,
    string? DocTypeCode = null,
    string? SriPaymentMethodCode = null
) : IRequest<Result<SalesInvoiceDto>>, IBranchScopedRequest;

public sealed record UpdateSalesDraftCommand(
    Guid Id,
    Guid CustomerId,
    DateOnly IssueDate,
    List<SalesLineInput> Lines,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    List<SalesPaymentInput>? Payments = null
) : IRequest<Result<SalesInvoiceDto>>, IBranchScopedRequest;

public sealed record GetSalesInvoiceByIdQuery(Guid Id)
    : IRequest<Result<SalesInvoiceDto>>,
        IBranchScopedRequest;

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto (defensa en profundidad vía
/// BranchScopeBehavior/IBranchAccessGuard) — SalesInvoice no tiene BranchId de cabecera, así
/// que esto no filtra resultados por sucursal, solo exige que el usuario opere con una
/// sucursal activa autorizada, igual que el resto de UseCases de Sales ya migrados.
/// </summary>
public sealed record GetSalesInvoiceListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<SalesListResponse>>, IBranchScopedRequest;

public sealed record SalesListResponse(
    IReadOnlyList<SalesListDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateSalesDraftValidator : AbstractValidator<CreateSalesDraftCommand>
{
    /// <summary>
    /// CLEAN-01C: <c>DocTypeCode</c> se persiste en <c>SalesInvoice</c> sin FK (a diferencia de
    /// <c>DocumentSequence</c>, que sí referencia <c>SriDocType</c>) — sin esta regla, un cliente
    /// podía enviar cualquier string de hasta 5 caracteres y quedaba guardado sin validar contra el
    /// catálogo fiscal real. Mismo patrón <c>MustAsync</c> que
    /// <c>UpdateSupplierClassificationConfigValidator</c> (CLASS-BP-CATALOGS-01).
    /// </summary>
    public CreateSalesDraftValidator(ISriDocTypeCatalogResolver docTypeCatalogResolver)
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.DocTypeCode)
            .MustAsync((code, ct) => docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(code!, ct))
            .WithMessage("El tipo de comprobante no corresponde a un código SRI activo.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocTypeCode));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(SalesInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad debe ser mayor a cero.");
                line.RuleFor(l => l.UnitPrice)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El precio no puede ser negativo.");
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
    }
}

public sealed class UpdateSalesDraftValidator : AbstractValidator<UpdateSalesDraftCommand>
{
    public UpdateSalesDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(SalesInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity).GreaterThan(0);
                line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateSalesDraftHandler
    : IRequestHandler<CreateSalesDraftCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPaymentTermRepository _ptRepo;
    private readonly IPaymentMethodRepository _pmRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPricingResolver _pricing;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICurrentCashSession _cashSession;

    public CreateSalesDraftHandler(
        ISalesInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IPaymentMethodRepository pmRepo,
        IItemRepository itemRepo,
        IEmissionPointRepository epRepo,
        ISriTaxResolver tax,
        IPricingResolver pricing,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        ICurrentCashSession cashSession
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _pmRepo = pmRepo;
        _itemRepo = itemRepo;
        _epRepo = epRepo;
        _tax = tax;
        _pricing = pricing;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _cashSession = cashSession;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        CreateSalesDraftCommand cmd,
        CancellationToken ct
    )
    {
        if (!_cashSession.HasOpenSession)
            return Result<SalesInvoiceDto>.ValidationFailure(
                "No existe una caja abierta para realizar ventas."
            );

        var bp = await _bpRepo.GetByIdAsync(cmd.CustomerId, ct);
        if (bp is null)
            return Result<SalesInvoiceDto>.NotFound("Cliente no encontrado.");
        if (!bp.IsActive)
            return Result<SalesInvoiceDto>.ValidationFailure("El cliente se encuentra inactivo.");

        var customerRole = await _roleRepo.GetByTypeAsync(
            cmd.CustomerId,
            Domain.MasterData.Enums.RoleType.Customer,
            ct
        );
        if (customerRole is null)
            return Result<SalesInvoiceDto>.ValidationFailure(
                "El socio de negocio no tiene rol de Cliente."
            );

        var ptId = cmd.PaymentTermId;
        if (ptId is null || ptId == Guid.Empty)
        {
            var pts = await _ptRepo.ListAsync(_t.TenantId, null, ct);
            var defaultPt = pts.FirstOrDefault();
            if (defaultPt is null)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    "No hay condiciones de pago configuradas."
                );
            ptId = defaultPt.Id;
        }
        var pt = await _ptRepo.GetByIdAsync(_t.TenantId, ptId.Value, ct);
        if (pt is null)
            return Result<SalesInvoiceDto>.ValidationFailure("La condición de pago no existe.");

        var tid = _t.TenantId;

        var customerSnapshot = CustomerSnapshot.Create(
            bp.Name.LegalName,
            bp.Identification.Number,
            bp.Identification.Type
        );

        var paymentTermSnapshot = PaymentTermSnapshot.Create(
            pt.Id,
            pt.Name,
            pt.Installments,
            pt.DaysBetweenInstallments
        );

        var docTypeCode = cmd.DocTypeCode?.Trim();
        if (string.IsNullOrEmpty(docTypeCode))
            docTypeCode = "01"; // Default: Factura (fuente de verdad: tabla sri_doc_types)

        // EmissionPointId nunca viene del cliente — proviene de la caja abierta del usuario
        // (ICurrentCashSession), garantizada no-nula por HasOpenSession (Fase 2: un CashRegister
        // no puede abrir sesión sin EmissionPointId asignado).
        var emissionPointId = _cashSession.EmissionPointId!.Value;
        var emissionType = EmissionType.Electronic;
        var ep = await _epRepo.GetByIdAsync(emissionPointId, tid, ct);
        if (ep is not null)
            emissionType = ep.EmissionType;

        var draftNumber = $"DRAFT-{Guid.NewGuid():N}"[..14];

        var inv = SalesInvoice.CreateDraft(
            tid,
            _c.CompanyId,
            _b.BranchId,
            cmd.CustomerId,
            customerSnapshot,
            draftNumber,
            cmd.IssueDate,
            _u.UserId,
            paymentTermSnapshot,
            cashSessionId: _cashSession.CashSessionId!.Value,
            docTypeCode: docTypeCode,
            emissionPointId: emissionPointId,
            emissionType: emissionType,
            dueDate: cmd.DueDate,
            notes: cmd.Notes,
            sriPaymentMethodCode: cmd.SriPaymentMethodCode
        );

        var linesResult = await SalesLineBuilder.BuildAsync(
            cmd.Lines,
            inv.Id,
            tid,
            _itemRepo,
            _tax,
            _pricing,
            ct
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        inv.ReplaceLines(linesResult.Lines, _u.UserId);

        if (cmd.Payments is { Count: > 0 })
        {
            var paymentsResult = await SalesPaymentHelper.BuildPaymentsAsync(
                cmd.Payments,
                inv.Id,
                tid,
                _pmRepo,
                ct
            );
            if (paymentsResult.Error is not null)
                return paymentsResult.Error;
            inv.ReplacePayments(paymentsResult.Items!, _u.UserId);
        }

        await _repo.AddAsync(inv, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}

public sealed class UpdateSalesDraftHandler
    : IRequestHandler<UpdateSalesDraftCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPaymentTermRepository _ptRepo;
    private readonly IPaymentMethodRepository _pmRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPricingResolver _pricing;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateSalesDraftHandler(
        ISalesInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IPaymentMethodRepository pmRepo,
        IItemRepository itemRepo,
        ISriTaxResolver tax,
        IPricingResolver pricing,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _pmRepo = pmRepo;
        _itemRepo = itemRepo;
        _tax = tax;
        _pricing = pricing;
        _t = t;
        _u = u;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        UpdateSalesDraftCommand cmd,
        CancellationToken ct
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.CustomerId, ct);
        if (bp is null)
            return Result<SalesInvoiceDto>.NotFound("Cliente no encontrado.");
        if (!bp.IsActive)
            return Result<SalesInvoiceDto>.ValidationFailure("El cliente se encuentra inactivo.");

        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (inv is null)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        if (cmd.PaymentTermId.HasValue && cmd.PaymentTermId.Value != inv.PaymentTerm.Id)
        {
            var pt = await _ptRepo.GetByIdAsync(_t.TenantId, cmd.PaymentTermId.Value, ct);
            if (pt is not null)
                inv.UpdatePaymentTerm(
                    PaymentTermSnapshot.Create(
                        pt.Id,
                        pt.Name,
                        pt.Installments,
                        pt.DaysBetweenInstallments
                    )
                );
        }
        else if (cmd.CustomerId != inv.CustomerId)
        {
            var pts = await _ptRepo.ListAsync(_t.TenantId, null, ct);
            var defaultPt = pts.FirstOrDefault();
            if (defaultPt is not null)
                inv.UpdatePaymentTerm(
                    PaymentTermSnapshot.Create(
                        defaultPt.Id,
                        defaultPt.Name,
                        defaultPt.Installments,
                        defaultPt.DaysBetweenInstallments
                    )
                );
        }

        try
        {
            var customerSnapshot = CustomerSnapshot.Create(
                bp.Name.LegalName,
                bp.Identification.Number,
                bp.Identification.Type
            );

            inv.UpdateDraft(
                cmd.CustomerId,
                customerSnapshot,
                cmd.IssueDate,
                _u.UserId,
                dueDate: cmd.DueDate,
                notes: cmd.Notes
            );

            var linesResult = await SalesLineBuilder.BuildAsync(
                cmd.Lines,
                inv.Id,
                _t.TenantId,
                _itemRepo,
                _tax,
                _pricing,
                ct
            );
            if (linesResult.Error is not null)
                return linesResult.Error;

            await _repo.RemoveLinesByInvoiceAsync(inv.Id, linesResult.Lines, ct);
            inv.ReplaceLines(linesResult.Lines, _u.UserId);

            if (cmd.Payments is { Count: > 0 })
            {
                var paymentsResult = await SalesPaymentHelper.BuildPaymentsAsync(
                    cmd.Payments,
                    inv.Id,
                    _t.TenantId,
                    _pmRepo,
                    ct
                );
                if (paymentsResult.Error is not null)
                    return paymentsResult.Error;
                await _repo.RemovePaymentsByInvoiceAsync(inv.Id, ct);
                inv.ReplacePayments(paymentsResult.Items!, _u.UserId);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}

public sealed class GetSalesInvoiceByIdHandler
    : IRequestHandler<GetSalesInvoiceByIdQuery, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository _edocRepo;
    private readonly ICurrentTenant _t;

    public GetSalesInvoiceByIdHandler(
        ISalesInvoiceRepository repo,
        ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository edocRepo,
        ICurrentTenant t
    )
    {
        _repo = repo;
        _edocRepo = edocRepo;
        _t = t;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        GetSalesInvoiceByIdQuery q,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (inv is null)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // Fase 10: ElectronicDocument es la única fuente de verdad del estado electrónico.
        var edoc = await _edocRepo.GetBySourceAsync(_t.TenantId, "Sales", inv.Id, ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv, edoc));
    }
}

public sealed class GetSalesInvoiceListHandler
    : IRequestHandler<GetSalesInvoiceListQuery, Result<SalesListResponse>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSalesInvoiceListHandler(ISalesInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesListResponse>> Handle(
        GetSalesInvoiceListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var dtos = items
            .Select(i => new SalesListDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.CustomerId,
                i.Customer.Name,
                i.Status.ToString(),
                i.Lines.Count,
                i.GrandTotal,
                i.CreatedAt
            ))
            .ToList();
        return Result<SalesListResponse>.Success(
            new SalesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

file static class SalesLineBuilder
{
    public static async Task<LinesBuildResult> BuildAsync(
        List<SalesLineInput> inputs,
        Guid invoiceId,
        Guid tid,
        IItemRepository itemRepo,
        ISriTaxResolver tax,
        IPricingResolver pricingResolver,
        CancellationToken ct
    )
    {
        var lines = new List<SalesInvoiceDetail>();
        foreach (var l in inputs)
        {
            var vatCode = l.VatCode;
            var iceCode = l.IceCode;
            string? snapshotSku = null;
            string? snapshotItemName = null;
            string uomCode = "UNIT";
            Guid? warehouseId = null;

            if (l.ItemId.HasValue)
            {
                var item = await itemRepo.GetByIdLightAsync(l.ItemId.Value, tid, ct);
                if (item is null)
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': el producto seleccionado ya no existe."
                        )
                    );

                if (!item.IsActive || !item.SaleConfig.IsForSale)
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': el producto '{item.Code.Description}' está inactivo o no está habilitado para venta."
                        )
                    );

                snapshotSku = item.Code.SKU;
                snapshotItemName = item.Code.Description;
                uomCode = item.DefaultUomCode;

                // Kardex: la bodega de despacho es obligatoria por línea cuando el ítem
                // controla inventario — una misma factura puede despachar de bodegas distintas.
                if (item.StockConfig.TracksStock)
                {
                    if (l.WarehouseId is null || l.WarehouseId == Guid.Empty)
                        return new(
                            null!,
                            Result<SalesInvoiceDto>.ValidationFailure(
                                $"Línea '{l.Description}': debe seleccionar la bodega de despacho para este producto."
                            )
                        );
                    warehouseId = l.WarehouseId;
                }

                // Configuración Tributaria CLOSED: el Item es la única fuente de verdad —
                // el VatCode/IceCode enviado por el cliente nunca prevalece sobre el ítem.
                vatCode = item.TaxConfig.SaleVatCode ?? vatCode;
                iceCode = item.TaxConfig.ExciseTaxCode;

                // Pricing Engine v2 (SSOT del precio de venta) — resuelve el precio vigente
                // para validar el piso de descuento configurado en el maestro del ítem.
                var pricingResult = await pricingResolver.ResolveAsync(item.Id, ct: ct);
                if (pricingResult.IsSuccess)
                {
                    var resolvedPrice = pricingResult.Value!.UnitPrice;
                    var maxDiscountPercent = item.SaleConfig.MaxDiscountPercent;
                    if (maxDiscountPercent.HasValue)
                    {
                        var minAllowed = resolvedPrice * (1 - maxDiscountPercent.Value / 100m);
                        if (l.UnitPrice < minAllowed)
                            return new(
                                null!,
                                Result<SalesInvoiceDto>.ValidationFailure(
                                    $"Línea '{l.Description}': el precio ingresado excede el descuento máximo permitido para este producto ({maxDiscountPercent.Value}%)."
                                )
                            );
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(vatCode))
                return new(
                    null!,
                    Result<SalesInvoiceDto>.ValidationFailure(
                        $"Línea '{l.Description}': código IVA obligatorio. Seleccione un producto con tarifa IVA o indique el código manualmente."
                    )
                );

            var line = SalesInvoiceDetail.Create(
                invoiceId,
                tid,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                vatCode,
                uomCode,
                l.ItemId,
                l.Notes,
                l.DiscountPct,
                iceCode,
                snapshotSku,
                snapshotItemName,
                warehouseId: warehouseId
            );

            var taxResult = await SalesTaxHelper.ResolveTaxesAsync(line, tax, ct);
            if (taxResult is not null)
                return new(null!, taxResult);
            lines.Add(line);
        }
        return new(lines, null);
    }

    public sealed record LinesBuildResult(
        List<SalesInvoiceDetail> Lines,
        Result<SalesInvoiceDto>? Error
    );
}

file static class SalesPaymentHelper
{
    public static async Task<PaymentsBuildResult> BuildPaymentsAsync(
        List<SalesPaymentInput> inputs,
        Guid invoiceId,
        Guid tenantId,
        IPaymentMethodRepository pmRepo,
        CancellationToken ct
    )
    {
        var items = new List<SalesInvoicePayment>();
        var cache = new Dictionary<Guid, PaymentMethod>();

        foreach (var input in inputs)
        {
            if (!cache.TryGetValue(input.PaymentMethodId, out var pm))
            {
                pm = await pmRepo.GetByIdAsync(tenantId, input.PaymentMethodId, ct);
                if (pm is null)
                    return PaymentsBuildResult.Fail(
                        $"Método de pago no encontrado (ID: {input.PaymentMethodId})."
                    );
                if (!pm.IsActive)
                    return PaymentsBuildResult.Fail(
                        $"El método de pago '{pm.Name}' está inactivo."
                    );
                if (pm.RequiresReference && string.IsNullOrWhiteSpace(input.Reference))
                    return PaymentsBuildResult.Fail(
                        $"El método '{pm.Name}' requiere una referencia."
                    );
                cache[pm.Id] = pm;
            }

            var payment = SalesInvoicePayment.Create(
                invoiceId,
                tenantId,
                pm.Id,
                pm.Code,
                pm.Name,
                input.Amount,
                input.Reference
            );

            if (input.CardDetail is not null)
                payment.SetCardDetail(
                    PaymentCardDetail.Create(
                        payment.Id,
                        input.CardDetail.CardBrand,
                        input.CardDetail.CardLastFour,
                        input.CardDetail.BankName,
                        input.CardDetail.AuthorizationCode,
                        input.CardDetail.LotNumber
                    )
                );

            if (input.TransferDetail is not null)
                payment.SetTransferDetail(
                    PaymentTransferDetail.Create(
                        payment.Id,
                        input.TransferDetail.BankName,
                        input.TransferDetail.ReceiptNumber,
                        ParseDate(input.TransferDetail.TransferDate)
                    )
                );

            if (input.ChequeDetail is not null)
                payment.SetChequeDetail(
                    PaymentChequeDetail.Create(
                        payment.Id,
                        input.ChequeDetail.BankName,
                        input.ChequeDetail.ChequeNumber,
                        input.ChequeDetail.HolderName,
                        ParseDate(input.ChequeDetail.CashDate)
                    )
                );

            items.Add(payment);
        }

        var paymentSum = items.Sum(p => p.Amount);
        if (paymentSum < 0)
            return PaymentsBuildResult.Fail("La suma de cobros no puede ser negativa.");

        return PaymentsBuildResult.Ok(items);
    }

    private static DateOnly? ParseDate(string? iso) =>
        iso is not null && DateOnly.TryParse(iso, out var d) ? d : null;

    public sealed record PaymentsBuildResult(
        List<SalesInvoicePayment>? Items,
        Result<SalesInvoiceDto>? Error
    )
    {
        public static PaymentsBuildResult Ok(List<SalesInvoicePayment> items) => new(items, null);

        public static PaymentsBuildResult Fail(string msg) =>
            new(null, Result<SalesInvoiceDto>.ValidationFailure(msg));
    }
}

file static class SalesTaxHelper
{
    public static async Task<Result<SalesInvoiceDto>?> ResolveTaxesAsync(
        SalesInvoiceDetail line,
        ISriTaxResolver tax,
        CancellationToken ct
    )
    {
        var vatResult = await tax.GetVatRateWithNameAsync(line.VatCode, ct);
        if (vatResult is null)
            return Result<SalesInvoiceDto>.ValidationFailure(
                $"Código IVA '{line.VatCode}' no encontrado o inactivo."
            );

        decimal iceRate = 0;
        string? iceName = null;
        if (!string.IsNullOrWhiteSpace(line.IceCode))
        {
            var iceResult = await tax.GetIceRateWithNameAsync(line.IceCode, ct);
            if (iceResult is null)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"Código ICE '{line.IceCode}' no encontrado o inactivo."
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
        return null;
    }
}

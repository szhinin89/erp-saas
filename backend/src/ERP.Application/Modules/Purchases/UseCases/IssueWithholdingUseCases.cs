using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.Services;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record IssuedWithholdingDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    Guid SupplierId,
    Guid EmissionPointId,
    string WithholdingNumber,
    DateOnly IssueDate,
    string? AccessKey,
    decimal TotalRetainedVat,
    decimal TotalRetainedIncome,
    decimal TotalRetainedIsd,
    decimal TotalRetained,
    string Status,
    IReadOnlyList<IssuedWithholdingDetailDto> Details,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record IssuedWithholdingDetailDto(
    Guid Id,
    string TaxType,
    string RetentionCode,
    string RetentionCodeDescription,
    decimal TaxableBase,
    decimal RetentionPct,
    decimal AmountRetained
);

// ── Command ─────────────────────────────────────────────────────────────

public sealed record IssueWithholdingCommand(
    Guid PurchaseInvoiceId,
    Guid EmissionPointId,
    DateOnly IssueDate
) : IRequest<Result<IssuedWithholdingDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class IssueWithholdingValidator : AbstractValidator<IssueWithholdingCommand>
{
    public IssueWithholdingValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
        RuleFor(x => x.EmissionPointId).NotEmpty();
        RuleFor(x => x.IssueDate).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class IssueWithholdingHandler
    : IRequestHandler<IssueWithholdingCommand, Result<IssuedWithholdingDto>>
{
    // SRI reporta la tolerancia como "129600 minutos" en el mensaje de rechazo [65]
    // FECHA EMISIÓN EXTEMPORANEA (129600 / 60 / 24 = 90 días). Mismo umbral verificado
    // ya para Ventas (ver AuthorizeSalesUseCases.cs) — aplica igual a Retenciones.
    private const int SriIssueDateToleranceDays = 90;

    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IRetentionCodeResolver _retResolver;
    private readonly IEmissionPointRepository _epRepo;
    private readonly IEstablishmentRepository _estRepo;
    private readonly IDocumentSequenceRepository _seqRepo;
    private readonly ERP.Application.Common.Services.ICompanyClock _companyClock;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public IssueWithholdingHandler(
        IPurchaseInvoiceRepository purchaseRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IRetentionCodeResolver retResolver,
        IEmissionPointRepository epRepo,
        IEstablishmentRepository estRepo,
        IDocumentSequenceRepository seqRepo,
        ERP.Application.Common.Services.ICompanyClock companyClock,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _purchaseRepo = purchaseRepo;
        _roleRepo = roleRepo;
        _retResolver = retResolver;
        _epRepo = epRepo;
        _estRepo = estRepo;
        _seqRepo = seqRepo;
        _companyClock = companyClock;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<IssuedWithholdingDto>> Handle(
        IssueWithholdingCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // ── Validar compra ──────────────────────────────────────────────
        var inv = await _purchaseRepo.GetByIdAsync(tid, cmd.PurchaseInvoiceId, ct);
        if (inv is null)
            return Result<IssuedWithholdingDto>.NotFound("Compra no encontrada.");
        if (inv.Status != Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed)
            return Result<IssuedWithholdingDto>.ValidationFailure(
                "Solo se pueden emitir retenciones de compras confirmadas."
            );

        // ── Validar fecha de emisión contra la fecha empresarial (Ecuador) ──
        // Corrige SRI [65] FECHA EMISIÓN EXTEMPORÁNEA: debe ejecutarse ANTES de capturar
        // secuencial — evita consumir un número de retención para una fecha que el SRI
        // rechazará. Usa ICompanyClock (zona horaria de la empresa), nunca DateTime.UtcNow.Date.
        var companyToday = await _companyClock.TodayAsync(_c.CompanyId, tid, ct);
        if (cmd.IssueDate > companyToday)
            return Result<IssuedWithholdingDto>.ValidationFailure(
                $"La fecha de emisión ({cmd.IssueDate:dd/MM/yyyy}) no puede ser posterior a la fecha actual ({companyToday:dd/MM/yyyy})."
            );
        if (cmd.IssueDate < companyToday.AddDays(-SriIssueDateToleranceDays))
            return Result<IssuedWithholdingDto>.ValidationFailure(
                $"La fecha de emisión ({cmd.IssueDate:dd/MM/yyyy}) excede el rango permitido por el SRI "
                    + $"({SriIssueDateToleranceDays} días)."
            );

        // ── Idempotencia: verificar que no exista retención activa ───────
        var existing = await _purchaseRepo.GetWithholdingByPurchaseIdAsync(
            tid,
            cmd.PurchaseInvoiceId,
            ct
        );
        if (
            existing is not null
            && existing.Status != Domain.Modules.Purchases.Enums.WithholdingStatus.Cancelled
        )
            return Result<IssuedWithholdingDto>.Conflict(
                "Esta compra ya tiene una retención emitida."
            );

        // ── Obtener config proveedor ────────────────────────────────────
        var supplierRole = await _roleRepo.GetByTypeAsync(inv.SupplierId, RoleType.Supplier, ct);
        var config = supplierRole?.SupplierConfig;
        if (config is not null && config.IsRetentionExempt)
            return Result<IssuedWithholdingDto>.ValidationFailure(
                "El proveedor está exento de retención."
            );

        // ── Calcular retención ──────────────────────────────────────────
        string? vatCode = config?.DefaultRetentionVatCode;
        decimal vatPct = 0;
        string? vatName = null;
        if (!string.IsNullOrWhiteSpace(vatCode))
        {
            var info = await _retResolver.GetRetentionCodeAsync(vatCode, "IVA", ct);
            if (info is not null)
            {
                vatPct = info.Percentage;
                vatName = info.Name;
            }
            else
                vatCode = null;
        }

        string? incomeCode = config?.DefaultRetentionIncomeCode;
        decimal incomePct = 0;
        string? incomeName = null;
        if (!string.IsNullOrWhiteSpace(incomeCode))
        {
            var info = await _retResolver.GetRetentionCodeAsync(incomeCode, "RENTA", ct);
            if (info is not null)
            {
                incomePct = info.Percentage;
                incomeName = info.Name;
            }
            else
                incomeCode = null;
        }

        var taxableBaseIncome = inv.Lines.Sum(l => l.TaxableBase);
        var calcResult = RetentionCalculator.Calculate(
            inv.TotalVat,
            taxableBaseIncome,
            false,
            vatCode,
            vatPct,
            vatName,
            incomeCode,
            incomePct,
            incomeName
        );

        if (calcResult.Lines.Count == 0)
            return Result<IssuedWithholdingDto>.ValidationFailure(
                calcResult.SkipReason ?? "No se generaron líneas de retención."
            );

        // ── Crear borrador ──────────────────────────────────────────────
        var wh = IssuedWithholding.CreateDraft(
            tid,
            _c.CompanyId,
            inv.Id,
            inv.SupplierId,
            cmd.EmissionPointId,
            cmd.IssueDate,
            uid
        );

        var details = calcResult.Lines.Select(l =>
            IssuedWithholdingDetail.Create(
                wh.Id,
                tid,
                l.TaxType,
                l.RetentionCode,
                l.RetentionCodeName,
                l.TaxableBase,
                l.RetentionPct
            )
        );
        wh.ReplaceDetails(details);

        // ── Generar número secuencial ───────────────────────────────────
        var ep = await _epRepo.GetByIdAsync(cmd.EmissionPointId, tid, ct);
        if (ep is null)
            return Result<IssuedWithholdingDto>.NotFound("Punto de emisión no encontrado.");

        var est = await _estRepo.GetByIdAsync(tid, ep.EstablishmentId, ct);
        if (est is null)
            return Result<IssuedWithholdingDto>.NotFound("Establecimiento no encontrado.");

        // CaptureNextAsync: atómico (advisory lock + transacción propia).
        // "07" es el código SRI fijo para retenciones en la fuente.
        var sequential = await _seqRepo.CaptureNextAsync(
            tid,
            _c.CompanyId,
            cmd.EmissionPointId,
            "07",
            ct
        );
        var number = $"{est.Code}-{ep.Code}-{sequential}";

        // ── Emitir ──────────────────────────────────────────────────────
        try
        {
            wh.Issue(number, uid);
        }
        catch (InvalidOperationException ex)
        {
            return Result<IssuedWithholdingDto>.ValidationFailure(ex.Message);
        }

        // ── Actualizar cuenta por pagar ─────────────────────────────────
        var payable = await _purchaseRepo.GetPayableByPurchaseIdAsync(tid, inv.Id, ct);
        if (payable is not null)
        {
            payable.ApplyRetention(wh.TotalRetained, inv.PaymentSchedules);
        }
        else if (wh.TotalRetained > 0)
        {
            return Result<IssuedWithholdingDto>.ValidationFailure(
                "No se encontró cuenta por pagar asociada. No se puede aplicar la retención financieramente."
            );
        }

        // ── Persistir (auditoría de "withholding.issued" vía IssuedWithholdingAuditHandler,
        // disparada por el domain event levantado en wh.Issue() dentro de este SaveChangesAsync) ──
        _purchaseRepo.TrackWithholding(wh);
        await _purchaseRepo.SaveChangesAsync(ct);
        return Result<IssuedWithholdingDto>.Success(MapWh.ToDto(wh));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class MapWh
{
    public static IssuedWithholdingDto ToDto(IssuedWithholding w) =>
        new(
            w.Id,
            w.PurchaseInvoiceId,
            w.SupplierId,
            w.EmissionPointId,
            w.WithholdingNumber,
            w.IssueDate,
            w.AccessKey,
            w.TotalRetainedVat,
            w.TotalRetainedIncome,
            w.TotalRetainedIsd,
            w.TotalRetained,
            w.Status.ToString(),
            w.Details.Select(d => new IssuedWithholdingDetailDto(
                    d.Id,
                    d.TaxType,
                    d.RetentionCode,
                    d.RetentionCodeDescription,
                    d.TaxableBase,
                    d.RetentionPct,
                    d.AmountRetained
                ))
                .ToList(),
            w.CreatedAt,
            w.UpdatedAt
        );
}

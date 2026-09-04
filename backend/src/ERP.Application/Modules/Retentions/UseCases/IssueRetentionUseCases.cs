using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Inputs ──────────────────────────────────────────────────────────────

/// <summary>
/// Línea de entrada del comando de emisión. El monto/porcentaje/base los decide el usuario en esta
/// fase (no hay <c>RetentionCalculator</c> invocado todavía desde este caso de uso) — lo que SÍ se
/// revalida siempre server-side es la ELEGIBILIDAD (paso 4 del handler), nunca el cálculo en sí.
/// </summary>
public sealed record IssueRetentionLineInput(
    RetentionTaxType TaxType,
    string RetentionCode,
    decimal BaseAmount,
    decimal RetentionRate,
    decimal RetainedAmount,
    string? Description = null
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-APPLICATION-01C — emite un <see cref="RetentionDocument"/> de forma AISLADA: no
/// integra con <c>ConfirmExpenseDocumentHandler</c>, no toca <c>AccountsPayable</c>, no genera
/// asiento contable, no modifica el <c>ExpenseDocument</c> origen. Esa integración transaccional es
/// de una fase posterior (ver <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Flujo
/// funcional integrado de retenciones").
///
/// <see cref="RetentionNumber"/> es obligatorio en esta fase: no existe todavía un <c>DocType</c>
/// sembrado para Retentions en <c>CaptureNextAsync</c>/<c>DocumentSequence</c> — la numeración
/// automática queda para la fase de integración (E1-B/E1-C). Nunca se hardcodea aquí un prefijo
/// tipo "RET".
///
/// <c>TenantId</c>/<c>CompanyId</c>/<c>BranchId</c> NUNCA viajan en este command — salen siempre
/// de <see cref="ICurrentTenant"/>/<see cref="ICurrentCompany"/>/<see cref="ICurrentBranch"/> en el
/// handler, mismo patrón que <c>ExpenseDocumentConfirmUseCases.cs</c>/<c>IssueWithholdingUseCases.cs</c>.
/// </summary>
public sealed record IssueRetentionCommand(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    Guid EmissionPointId,
    string RetentionNumber,
    DateOnly IssueDate,
    IReadOnlyList<IssueRetentionLineInput> Lines
) : IRequest<Result<RetentionDocumentDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class IssueRetentionLineValidator : AbstractValidator<IssueRetentionLineInput>
{
    public IssueRetentionLineValidator()
    {
        RuleFor(x => x.RetentionCode).NotEmpty();
        RuleFor(x => x.BaseAmount).GreaterThan(0);
        RuleFor(x => x.RetentionRate).GreaterThan(0);
        RuleFor(x => x.RetainedAmount).GreaterThan(0);
        RuleFor(x => x).Must(l => l.RetainedAmount <= l.BaseAmount)
            .WithMessage("El monto retenido no puede ser mayor a la base imponible.");
    }
}

public sealed class IssueRetentionValidator : AbstractValidator<IssueRetentionCommand>
{
    public IssueRetentionValidator()
    {
        RuleFor(x => x.SourceDocumentId).NotEmpty();
        RuleFor(x => x.SourceDocumentType).IsInEnum();
        RuleFor(x => x.EmissionPointId).NotEmpty();
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.RetentionNumber).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new IssueRetentionLineValidator());
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class IssueRetentionHandler : IRequestHandler<IssueRetentionCommand, Result<RetentionDocumentDto>>
{
    private readonly IExpenseDocumentRepository _expenseRepo;
    private readonly IRetentionDocumentRepository _retentionRepo;
    private readonly IRetentionEligibilityService _eligibilityService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public IssueRetentionHandler(
        IExpenseDocumentRepository expenseRepo,
        IRetentionDocumentRepository retentionRepo,
        IRetentionEligibilityService eligibilityService,
        IUnitOfWork uow,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _expenseRepo = expenseRepo;
        _retentionRepo = retentionRepo;
        _eligibilityService = eligibilityService;
        _uow = uow;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<RetentionDocumentDto>> Handle(IssueRetentionCommand cmd, CancellationToken ct)
    {
        var tid = _tenant.TenantId;
        var cid = _company.CompanyId;
        var bid = _branch.BranchId;
        var uid = _user.UserId;

        // 1) PurchaseInvoice/Manual: fuera de alcance de esta fase (Compras sigue con
        // IssuedWithholding sin cambios; Manual está reservado sin implementación). Resultado
        // explícito de "no soportado", nunca confundido con "no elegible por regla fiscal" — mismo
        // criterio ya usado por GetRetentionEligibilityHandler.
        if (cmd.SourceDocumentType != RetentionSourceDocumentType.ExpenseDocument)
        {
            return Result<RetentionDocumentDto>.ValidationFailure(
                $"NotSupportedInThisPhase: la emisión de retención para {cmd.SourceDocumentType} "
                    + "no está implementada en RETENTIONS-APPLICATION-01C."
            );
        }

        // 2) Cargar y validar el ExpenseDocument origen, fail-closed tenant/company/branch.
        // GetByIdAsync ya filtra por tenant+company (ForOperationalScope) — el branch se valida
        // explícitamente aquí porque el repositorio no lo filtra, mismo patrón exacto que
        // GetRetentionEligibilityHandler/CancelExpenseDocumentHandler (nunca IgnoreQueryFilters).
        var document = await _expenseRepo.GetByIdAsync(tid, cmd.SourceDocumentId, ct);
        if (document is null || document.BranchId != bid)
            return Result<RetentionDocumentDto>.NotFound("Documento de gasto no encontrado.");

        if (document.Status != ExpenseStatus.Confirmed)
            return Result<RetentionDocumentDto>.ValidationFailure(
                "Solo se puede emitir una retención sobre un gasto confirmado."
            );

        // 3) Unicidad por origen — nunca crear una segunda retención activa sobre el mismo origen
        // (ver docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Agregado raíz").
        var alreadyExists = await _retentionRepo.ExistsActiveBySourceAsync(
            tid,
            cid,
            cmd.SourceDocumentType,
            cmd.SourceDocumentId,
            ct
        );
        if (alreadyExists)
            return Result<RetentionDocumentDto>.Conflict(
                "Ya existe una retención activa para este documento origen."
            );

        // 4) Revalidar elegibilidad server-side con la base retenible real del ExpenseDocument —
        // nunca confía en las líneas que el usuario envió como prueba de que aplica. Misma
        // resolución de bases que GetRetentionEligibilityHandler (TotalVat / suma de TaxableBase).
        var eligibility = await _eligibilityService.EvaluateAsync(
            tid,
            cid,
            document.SupplierId,
            document.TotalVat,
            document.Lines.Sum(l => l.TaxableBase),
            ct
        );

        var wantsVat = cmd.Lines.Any(l => l.TaxType == RetentionTaxType.Vat);
        var wantsIncome = cmd.Lines.Any(l => l.TaxType == RetentionTaxType.Income);

        if (wantsVat && !eligibility.CanRetainVat)
            return Result<RetentionDocumentDto>.ValidationFailure(
                string.Join(" ", eligibility.Reasons)
            );
        if (wantsIncome && !eligibility.CanRetainIncome)
            return Result<RetentionDocumentDto>.ValidationFailure(
                string.Join(" ", eligibility.Reasons)
            );

        // 5)-7) Crear el documento en Draft con los IDs del contexto seguro, agregar líneas y
        // emitir. NO se toca AccountsPayable/JournalEntry/ExpenseDocument en esta fase — esta
        // emisión queda completamente aislada (ver comentario de tipo del command).
        RetentionDocument retention;
        try
        {
            retention = RetentionDocument.Create(
                tid,
                cid,
                bid,
                cmd.SourceDocumentType,
                cmd.SourceDocumentId,
                document.SupplierId,
                cmd.EmissionPointId,
                uid
            );

            foreach (var line in cmd.Lines)
            {
                retention.AddLine(
                    RetentionDocumentLine.Create(
                        retention.Id,
                        tid,
                        line.TaxType,
                        line.RetentionCode,
                        line.BaseAmount,
                        line.RetentionRate,
                        line.RetainedAmount,
                        line.Description
                    )
                );
            }

            retention.Issue(cmd.RetentionNumber, cmd.IssueDate, uid);
        }
        catch (ArgumentException ex)
        {
            return Result<RetentionDocumentDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RetentionDocumentDto>.ValidationFailure(ex.Message);
        }

        // 8) Persistir. Sin BeginTransactionAsync explícito — un único agregado nuevo
        // (RetentionDocument + sus líneas), sin tocar ningún otro agregado en esta fase.
        await _retentionRepo.AddAsync(retention, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<RetentionDocumentDto>.Success(RetentionDocumentMapper.ToDto(retention));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

/// <summary>
/// internal (no file-scoped) para que <c>CancelRetentionUseCases.cs</c>/<c>GetRetentionBySourceUseCases.cs</c>
/// reutilicen el mismo mapeo — una sola fuente de verdad para <see cref="RetentionDocumentDto"/>,
/// mismo criterio que <c>ExpenseDocumentMapper</c>.
/// </summary>
internal static class RetentionDocumentMapper
{
    public static RetentionDocumentDto ToDto(RetentionDocument document) =>
        new(
            document.Id,
            document.CompanyId,
            document.BranchId,
            document.SourceDocumentType,
            document.SourceDocumentId,
            document.SubjectBusinessPartnerId,
            document.EmissionPointId,
            document.RetentionNumber,
            document.IssueDate,
            document.Status,
            document.TotalRetainedVat,
            document.TotalRetainedIncome,
            document.TotalRetained,
            document.CancelReason,
            document.CancelledAt,
            document.CancelledBy,
            document.Lines
                .Select(l => new RetentionDocumentLineDto(
                    l.Id,
                    l.TaxType,
                    l.RetentionCode,
                    l.BaseAmount,
                    l.RetentionRate,
                    l.RetainedAmount,
                    l.Description
                ))
                .ToList()
        );
}

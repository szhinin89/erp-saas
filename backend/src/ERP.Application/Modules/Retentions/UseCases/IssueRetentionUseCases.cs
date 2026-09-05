using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
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
    string? Description = null,
    // RETENTIONS-TAX-COMPONENT-MODEL-02B: snapshot requerido a nivel de dominio
    // (RetentionDocumentLine.RetentionCodeDescription), pero OPCIONAL aquí para no romper el
    // contrato de API/frontend actual — ExpenseRetentionSection.tsx no tiene todavía un selector
    // de catálogo que resuelva este texto. Si viene null/vacío, RetentionIssuer usa RetentionCode
    // como descripción de respaldo. Mejorar cuando exista ese selector real en frontend.
    string? RetentionCodeDescription = null
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-APPLICATION-01C — emite un <see cref="RetentionDocument"/> de forma AISLADA (post-
/// confirmación del documento origen, nunca integrada en su propia transacción de confirmación):
/// para <see cref="RetentionSourceDocumentType.ExpenseDocument"/> no toca <c>AccountsPayable</c> ni
/// genera asiento por su cuenta (esa integración transaccional vive en
/// <c>ConfirmExpenseDocumentHandler</c>/<c>CreateConfirmedExpenseHandler</c>, ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Flujo funcional integrado de
/// retenciones").
///
/// PURCHASES-RETENTIONS-BRIDGE-05B — <see cref="RetentionSourceDocumentType.PurchaseInvoice"/> SÍ
/// queda soportado aquí (a diferencia de Gastos, Compras nunca integra la emisión dentro de
/// confirmar la compra: siempre es post-confirmación): resuelve la <c>AccountsPayable</c> ya
/// existente del origen y le aplica <c>ApplyRetention</c> en esta misma operación — el asiento
/// contable (<c>Retentions/DocumentIssued</c>) se dispara solo, vía el mismo
/// <c>RetentionDocumentIssuedEvent</c> que ya escucha <c>RetentionDocumentIssuedPostingTranslator</c>
/// (genérico, no distingue origen).
///
/// <see cref="RetentionNumber"/>: RETENTIONS-DOCUMENT-SEQUENCE-02E — ya no viaja en este command.
/// <see cref="RetentionIssuer"/> lo genera internamente vía
/// <see cref="ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository.CaptureNextAsync"/>
/// a partir de <see cref="EmissionPointId"/> — nunca un valor enviado por el cliente.
///
/// <c>TenantId</c>/<c>CompanyId</c>/<c>BranchId</c> NUNCA viajan en este command — salen siempre
/// de <see cref="ICurrentTenant"/>/<see cref="ICurrentCompany"/>/<see cref="ICurrentBranch"/> en el
/// handler, mismo patrón que <c>ExpenseDocumentConfirmUseCases.cs</c>.
/// </summary>
public sealed record IssueRetentionCommand(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    Guid EmissionPointId,
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
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new IssueRetentionLineValidator());
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class IssueRetentionHandler : IRequestHandler<IssueRetentionCommand, Result<RetentionDocumentDto>>
{
    private readonly IExpenseDocumentRepository _expenseRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IRetentionIssuer _issuer;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public IssueRetentionHandler(
        IExpenseDocumentRepository expenseRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        IPurchaseReturnRepository purchaseReturnRepo,
        IAccountsPayableRepository payableRepo,
        IRetentionIssuer issuer,
        IUnitOfWork uow,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _expenseRepo = expenseRepo;
        _purchaseRepo = purchaseRepo;
        _purchaseReturnRepo = purchaseReturnRepo;
        _payableRepo = payableRepo;
        _issuer = issuer;
        _uow = uow;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public Task<Result<RetentionDocumentDto>> Handle(IssueRetentionCommand cmd, CancellationToken ct) =>
        cmd.SourceDocumentType switch
        {
            RetentionSourceDocumentType.ExpenseDocument => HandleExpenseAsync(cmd, ct),
            // PURCHASES-RETENTIONS-BRIDGE-05B — Compras conectada al modelo transversal.
            RetentionSourceDocumentType.PurchaseInvoice => HandlePurchaseAsync(cmd, ct),
            // Manual: reservado sin implementación (RetentionSourceDocumentType.Manual). Resultado
            // explícito de "no soportado", nunca confundido con "no elegible por regla fiscal" —
            // mismo criterio ya usado por GetRetentionEligibilityHandler.
            _ => Task.FromResult(
                Result<RetentionDocumentDto>.ValidationFailure(
                    $"NotSupportedInThisPhase: la emisión de retención para {cmd.SourceDocumentType} "
                        + "no está implementada."
                )
            ),
        };

    private async Task<Result<RetentionDocumentDto>> HandleExpenseAsync(
        IssueRetentionCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var cid = _company.CompanyId;
        var bid = _branch.BranchId;
        var uid = _user.UserId;

        // Cargar y validar el ExpenseDocument origen, fail-closed tenant/company/branch.
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

        // RETENTIONS-EXPENSES-INTEGRATION-01D-1: la unicidad por origen, la revalidación de
        // elegibilidad server-side y la construcción/emisión del agregado se extrajeron a
        // IRetentionIssuer (ERP.Application/Modules/Retentions/Services/RetentionIssuer.cs) — la
        // misma operación que usa ConfirmExpenseDocumentHandler/CreateConfirmedExpenseHandler para
        // emitir la retención dentro de su propia transacción de confirmación. Este handler sigue
        // siendo la única vía de emisión AISLADA (post-confirmación, fuera de la transacción de
        // confirmar el gasto) — no se duplica lógica, solo se reutiliza. Deliberadamente NO aplica
        // AccountsPayable.ApplyRetention aquí (mismo comportamiento de siempre) — esa integración
        // transaccional para Gastos sigue viviendo exclusivamente en los handlers de Confirmar.
        var issued = await _issuer.IssueForExpenseAsync(
            document,
            new RetentionIssueRequest(tid, cid, bid, uid, cmd.EmissionPointId, cmd.IssueDate, cmd.Lines),
            ct
        );
        if (!issued.IsSuccess)
            return Result<RetentionDocumentDto>.Failure(issued.Error!, issued.Code);

        // Persistir. Sin BeginTransactionAsync explícito — un único agregado nuevo (RetentionDocument
        // + sus líneas, ya en staging vía IRetentionIssuer), sin tocar ningún otro agregado.
        await _uow.SaveChangesAsync(ct);

        return Result<RetentionDocumentDto>.Success(RetentionDocumentMapper.ToDto(issued.Value!));
    }

    /// <summary>
    /// PURCHASES-RETENTIONS-BRIDGE-05B / PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E — emite un
    /// <see cref="RetentionDocument"/> con <see cref="RetentionSourceDocumentType.PurchaseInvoice"/>,
    /// única vía de emisión para Compras tras retirar el legacy <c>IssuedWithholding</c>. Adquiere el
    /// mismo Lock A (<c>"PurchaseInvoice.FinancialLock"</c>) que <c>CancelPurchaseHandler</c> y que
    /// adquiría el legacy <c>IssueWithholdingHandler</c>, serializando esta emisión contra cualquier
    /// otra mutación financiera concurrente sobre la misma factura (pago, devolución, anulación).
    /// </summary>
    private async Task<Result<RetentionDocumentDto>> HandlePurchaseAsync(
        IssueRetentionCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var cid = _company.CompanyId;
        var bid = _branch.BranchId;
        var uid = _user.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _purchaseReturnRepo.AcquireFinancialLockAsync(tid, cmd.SourceDocumentId, ct);

            // Cargar y validar la PurchaseInvoice origen (recarga autoritativa bajo lock),
            // fail-closed tenant/company/branch — mismo criterio exacto que el bloque de
            // ExpenseDocument (GetByIdAsync ya filtra tenant+company, branch se valida
            // explícitamente porque el repositorio no lo filtra).
            var invoice = await _purchaseRepo.GetByIdAsync(tid, cmd.SourceDocumentId, ct);
            if (invoice is null || invoice.BranchId != bid)
            {
                await _uow.RollbackAsync(ct);
                return Result<RetentionDocumentDto>.NotFound("Compra no encontrada.");
            }

            if (invoice.Status != PurchaseStatus.Confirmed)
            {
                await _uow.RollbackAsync(ct);
                return Result<RetentionDocumentDto>.ValidationFailure(
                    "Solo se puede emitir una retención sobre una compra confirmada."
                );
            }

            // Núcleo de emisión genérico (unicidad por RetentionDocument.ExistsActiveBySourceAsync
            // ya la aplica IRetentionIssuer.IssueAsync, sin duplicar el chequeo aquí).
            var issued = await _issuer.IssueAsync(
                new RetentionSourceDocumentData(
                    RetentionSourceDocumentType.PurchaseInvoice,
                    invoice.Id,
                    invoice.SupplierId,
                    invoice.TotalVat,
                    invoice.Lines.Sum(l => l.TaxableBase),
                    new RetentionDocument.SourceDocumentSnapshot(
                        invoice.DocTypeCode,
                        invoice.InvoiceNumber,
                        invoice.IssueDate,
                        invoice.AuthorizationNumber,
                        invoice.TaxSupportCode,
                        invoice.Subtotal,
                        invoice.GrandTotal
                    )
                ),
                new RetentionIssueRequest(tid, cid, bid, uid, cmd.EmissionPointId, cmd.IssueDate, cmd.Lines),
                ct
            );
            if (!issued.IsSuccess)
            {
                await _uow.RollbackAsync(ct);
                return Result<RetentionDocumentDto>.Failure(issued.Error!, issued.Code);
            }

            // Aplicar la retención a la CxP ya existente de la compra. A diferencia de Gastos (que
            // crea/stagea su CxP en la misma transacción de confirmación), Compras siempre tiene ya
            // una AccountsPayable confirmada de antes — si no existe, es un estado de datos
            // inconsistente y se rechaza en vez de emitir una retención sin efecto financiero real.
            var payable = await _payableRepo.GetByOriginAsync(
                tid,
                cid,
                AccountsPayableOriginType.PurchaseInvoice,
                invoice.Id,
                ct
            );
            if (payable is not null)
            {
                if (issued.Value!.TotalRetained > 0)
                    payable.ApplyRetention(issued.Value.TotalRetained, uid);
            }
            else if (issued.Value!.TotalRetained > 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<RetentionDocumentDto>.ValidationFailure(
                    "No se encontró cuenta por pagar asociada. No se puede aplicar la retención financieramente."
                );
            }

            // Persistir en la misma transacción explícita abierta arriba: el RetentionDocument
            // nuevo (staged por IRetentionIssuer) y la mutación de AccountsPayable ya trackeada.
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return Result<RetentionDocumentDto>.Success(RetentionDocumentMapper.ToDto(issued.Value!));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<RetentionDocumentDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
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
                    l.Description,
                    l.RetentionCodeDescription
                ))
                .ToList(),
            document.FiscalPeriod,
            document.SourceDocumentSriTypeCode,
            document.SourceDocumentNumber,
            document.SourceDocumentIssueDate,
            document.SourceDocumentAuthorizationNumber,
            document.SourceDocumentTaxSupportCode,
            document.SourceDocumentSubtotal,
            document.SourceDocumentTotal
        );
}

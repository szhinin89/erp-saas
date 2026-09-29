using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Input ──────────────────────────────────────────────────────────────

public sealed record CashClosingCountInput(
    decimal DenominationValue,
    string DenominationLabel,
    int Quantity
);

// ── Command ────────────────────────────────────────────────────────────

public sealed record CloseCashSessionCommand(
    Guid Id,
    List<CashClosingCountInput> ClosingCounts,
    string? CloseNotes = null
) : IRequest<Result<CashSessionDto>>, IBranchScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class CloseCashSessionValidator : AbstractValidator<CloseCashSessionCommand>
{
    public CloseCashSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El ID de la sesión de caja es obligatorio.");
        RuleFor(x => x.ClosingCounts)
            .NotEmpty()
            .WithMessage("Debe incluir al menos una denominación en el arqueo.");
        RuleForEach(x => x.ClosingCounts)
            .ChildRules(count =>
            {
                count
                    .RuleFor(c => c.DenominationValue)
                    .GreaterThan(0)
                    .WithMessage("El valor de la denominación debe ser mayor a cero.");
                count
                    .RuleFor(c => c.DenominationLabel)
                    .NotEmpty()
                    .MaximumLength(CashClosingCount.DenominationLabelMaxLen)
                    .WithMessage("La etiqueta de denominación es obligatoria.");
                count
                    .RuleFor(c => c.Quantity)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("La cantidad no puede ser negativa.");
            });
        RuleFor(x => x.CloseNotes)
            .MaximumLength(CashSession.CloseNotesMaxLen)
            .WithMessage(
                $"Las notas de cierre no pueden exceder {CashSession.CloseNotesMaxLen} caracteres."
            );
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class CloseCashSessionHandler
    : IRequestHandler<CloseCashSessionCommand, Result<CashSessionDto>>
{
    private readonly ICashSessionRepository _repo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ICashRegisterRepository _crRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IOperationalPreferencesResolver _preferences;
    private readonly ICashFundingRequestRepository _fundingRequests;
    private readonly IUnitOfWork _uow;

    public CloseCashSessionHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences,
        ICashFundingRequestRepository fundingRequests,
        IUnitOfWork uow
    )
    {
        _fundingRequests = fundingRequests;
        _uow = uow;
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _t = t;
        _b = b;
        _u = u;
        _preferences = preferences;
    }

    /// <summary>Motivo de sistema estable de las solicitudes de efectivo canceladas al cerrar la caja.</summary>
    public const string ClosedCashRegisterReason = "Caja cerrada";

    private async Task<Result<CashSession>> CloseInTransactionAsync(CloseCashSessionCommand cmd, CancellationToken ct)
    {
        var session = await _repo.GetByIdForUpdateAsync(_t.TenantId, cmd.Id, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashSession>.NotFound("Sesión de caja no encontrada.");

        // 02B — `caja.close` decide QUÉ puede hacer el usuario; solo quien abrió la sesión la
        // cierra (CashSession.UserId). Fail-closed, sin bypass por rol ni por `caja.manage`.
        if (!session.IsControlledBy(_u.UserId))
            return Result<CashSession>.ValidationFailure(CashSessionOwnership.RejectionMessage(session));

        var closingCounts = cmd
            .ClosingCounts.Where(c => c.Quantity > 0)
            .Select(c =>
                CashClosingCount.Create(
                    session.Id,
                    session.TenantId,
                    c.DenominationValue,
                    c.DenominationLabel,
                    c.Quantity
                )
            )
            .ToList();

        // Solicitudes de efectivo pendientes de esta sesión: se cancelan en la misma transacción
        // (nunca se tocan las terminales). Tras el cierre ninguna podría atenderse.
        foreach (var pending in await _fundingRequests.ListPendingBySessionForUpdateAsync(_t.TenantId, session.Id, ct))
            pending.Cancel(_u.UserId, ClosedCashRegisterReason);

        try
        {
            session.Close(_u.UserId, closingCounts, cmd.CloseNotes);
        }
        catch (DomainRuleViolationException ex)
        {
            return Result<CashSession>.FromDomainRule(ex);
        }

        // CONFIG-DYNAMIC-OPERATIONS-01/02 (cash.allow_close_with_difference / max_allowed_difference /
        // require_reason_for_difference): se valida DESPUÉS de Close() (que es quien calcula
        // Difference) pero ANTES de SaveChangesAsync — así la mutación en memoria de
        // session.Close() nunca se persiste si el cierre no cumple la preferencia, sin necesidad
        // de un método aparte "dry-run" en el dominio.
        var preferences = await _preferences.ResolveAsync(ct);
        if (session.Difference is not (null or 0m))
        {
            if (!preferences.Cash.AllowCloseWithDifference)
                return Result<CashSession>.ValidationFailure(
                    "Esta empresa no permite cerrar la caja con diferencia. Ajuste el arqueo antes de continuar."
                );

            if (
                preferences.Cash.MaxAllowedDifference > 0m
                && Math.Abs(session.Difference.Value) > preferences.Cash.MaxAllowedDifference
            )
                return Result<CashSession>.ValidationFailure(
                    $"La diferencia del arqueo ({session.Difference.Value}) supera el máximo permitido ({preferences.Cash.MaxAllowedDifference})."
                );

            if (
                preferences.Cash.RequireReasonForDifference
                && string.IsNullOrWhiteSpace(cmd.CloseNotes)
            )
                return Result<CashSession>.ValidationFailure(
                    "Debe indicar un motivo en las notas de cierre porque el arqueo presenta una diferencia."
                );
        }

        return Result<CashSession>.Success(session);
    }

    public async Task<Result<CashSessionDto>> Handle(
        CloseCashSessionCommand cmd,
        CancellationToken ct
    )
    {
        // ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — una sola transacción: CashSession FOR UPDATE →
        // solicitudes de efectivo Pending de la sesión FOR UPDATE (orden por Id) → cancelarlas
        // ("Caja cerrada") → cerrar → commit. El lock de la sesión serializa el cierre contra
        // Fulfill/Create/Reject/Cancel (mismo orden único de locks).
        await _uow.BeginTransactionAsync(ct);
        CashSession session;
        try
        {
            var closed = await CloseInTransactionAsync(cmd, ct);
            if (!closed.IsSuccess)
            {
                await _uow.RollbackAsync(ct);
                return Result<CashSessionDto>.Failure(closed.Error!, closed.Code);
            }
            session = closed.Value!;
            await _repo.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }

        var ep = await _epRepo.GetByIdAsync(session.EmissionPointId, _t.TenantId, ct);
        var register = await _crRepo.GetByIdAsync(_t.TenantId, session.CashRegisterId, ct);
        return Result<CashSessionDto>.Success(
            CajaMapper.ToDto(
                session,
                ep?.EmissionType.ToString(),
                (register?.DefaultWarehouseId, register?.DefaultWarehouse?.Name),
                (register?.DefaultCustomerId, register?.DefaultCustomer?.Name.LegalName)
            )
        );
    }
}

using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Configuration.Interfaces;
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

    public CloseCashSessionHandler(
        ICashSessionRepository repo,
        IEmissionPointRepository epRepo,
        ICashRegisterRepository crRepo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences
    )
    {
        _repo = repo;
        _epRepo = epRepo;
        _crRepo = crRepo;
        _t = t;
        _b = b;
        _u = u;
        _preferences = preferences;
    }

    public async Task<Result<CashSessionDto>> Handle(
        CloseCashSessionCommand cmd,
        CancellationToken ct
    )
    {
        var session = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashSessionDto>.NotFound("Sesión de caja no encontrada.");

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

        try
        {
            session.Close(_u.UserId, closingCounts, cmd.CloseNotes);
        }
        catch (InvalidOperationException ex)
        {
            return Result<CashSessionDto>.ValidationFailure(ex.Message);
        }

        // CONFIG-DYNAMIC-OPERATIONS-01 (cash.require_reason_for_difference): se valida DESPUÉS de
        // Close() (que es quien calcula Difference) pero ANTES de SaveChangesAsync — así la
        // mutación en memoria de session.Close() nunca se persiste si falta el motivo, sin
        // necesidad de un método aparte "dry-run" en el dominio.
        var preferences = await _preferences.ResolveAsync(ct);
        if (
            preferences.Cash.RequireReasonForDifference
            && session.Difference is not (null or 0m)
            && string.IsNullOrWhiteSpace(cmd.CloseNotes)
        )
        {
            return Result<CashSessionDto>.ValidationFailure(
                "Debe indicar un motivo en las notas de cierre porque el arqueo presenta una diferencia."
            );
        }

        await _repo.SaveChangesAsync(ct);

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

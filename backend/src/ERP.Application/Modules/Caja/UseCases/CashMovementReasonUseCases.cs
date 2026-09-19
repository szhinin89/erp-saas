using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── TREASURY-CASH-MANUAL-MOVEMENTS-01 ───────────────────────────────────
// Catálogo administrable de motivos de movimiento manual de caja. Scope obligatorio
// Tenant+Company vía ICompanyScopedRequest (CompanyId siempre de ICurrentCompany, nunca del
// body) — un tenant con varias empresas nunca comparte motivos entre ellas.

// ── Queries ────────────────────────────────────────────────────────────

/// <summary>
/// <paramref name="MovementType"/> filtra por el tipo exacto (para poblar el select de
/// "Registrar movimiento" según el Tipo ya elegido) — omitido trae todos los tipos.
/// </summary>
public sealed record GetCashMovementReasonsQuery(
    string? MovementType = null,
    bool IncludeInactive = false
) : IRequest<Result<IReadOnlyList<CashMovementReasonDto>>>, ICompanyScopedRequest;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreateCashMovementReasonCommand(
    string Code,
    string Name,
    string MovementType,
    int SortOrder = 0
) : IRequest<Result<CashMovementReasonDto>>, ICompanyScopedRequest;

public sealed record UpdateCashMovementReasonCommand(
    Guid Id,
    string Name,
    string MovementType,
    int SortOrder
) : IRequest<Result<CashMovementReasonDto>>, ICompanyScopedRequest;

public sealed record ToggleCashMovementReasonCommand(Guid Id)
    : IRequest<Result<CashMovementReasonDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

file static class MovementTypeValidation
{
    public static bool IsManualType(string value) =>
        Enum.TryParse<CashMovementType>(value, true, out var t)
        && t is CashMovementType.ManualIncome or CashMovementType.ManualExpense or CashMovementType.Withdrawal;
}

public sealed class CreateCashMovementReasonValidator
    : AbstractValidator<CreateCashMovementReasonCommand>
{
    public CreateCashMovementReasonValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CashMovementReason.CodeMaxLen);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CashMovementReason.NameMaxLen);
        RuleFor(x => x.MovementType)
            .Must(MovementTypeValidation.IsManualType)
            .WithMessage("MovementType debe ser ManualIncome, ManualExpense o Withdrawal.");
    }
}

public sealed class UpdateCashMovementReasonValidator
    : AbstractValidator<UpdateCashMovementReasonCommand>
{
    public UpdateCashMovementReasonValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CashMovementReason.NameMaxLen);
        RuleFor(x => x.MovementType)
            .Must(MovementTypeValidation.IsManualType)
            .WithMessage("MovementType debe ser ManualIncome, ManualExpense o Withdrawal.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetCashMovementReasonsHandler
    : IRequestHandler<GetCashMovementReasonsQuery, Result<IReadOnlyList<CashMovementReasonDto>>>
{
    private readonly ICashMovementReasonRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetCashMovementReasonsHandler(
        ICashMovementReasonRepository repo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<CashMovementReasonDto>>> Handle(
        GetCashMovementReasonsQuery q,
        CancellationToken ct
    )
    {
        CashMovementType? movementType = null;
        if (!string.IsNullOrWhiteSpace(q.MovementType))
        {
            if (!Enum.TryParse<CashMovementType>(q.MovementType, true, out var parsed))
                return Result<IReadOnlyList<CashMovementReasonDto>>.ValidationFailure(
                    $"Tipo de movimiento '{q.MovementType}' no válido."
                );
            movementType = parsed;
        }

        var items = await _repo.ListAsync(
            _t.TenantId,
            _c.CompanyId,
            movementType,
            q.IncludeInactive,
            ct
        );

        return Result<IReadOnlyList<CashMovementReasonDto>>.Success(
            items.Select(CajaMapper.ToDto).ToList()
        );
    }
}

public sealed class CreateCashMovementReasonHandler
    : IRequestHandler<CreateCashMovementReasonCommand, Result<CashMovementReasonDto>>
{
    private readonly ICashMovementReasonRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateCashMovementReasonHandler(
        ICashMovementReasonRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CashMovementReasonDto>> Handle(
        CreateCashMovementReasonCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;

        var existing = await _repo.GetByCodeAsync(tid, cid, cmd.Code, ct);
        if (existing is not null)
            return Result<CashMovementReasonDto>.UniqueViolation(
                $"Ya existe un motivo con el código '{cmd.Code}' en esta empresa."
            );

        var movementType = Enum.Parse<CashMovementType>(cmd.MovementType, true);
        var reason = CashMovementReason.Create(
            tid,
            cid,
            cmd.Code,
            cmd.Name,
            movementType,
            cmd.SortOrder,
            _u.UserId
        );

        await _repo.AddAsync(reason, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CashMovementReasonDto>.Success(CajaMapper.ToDto(reason));
    }
}

public sealed class UpdateCashMovementReasonHandler
    : IRequestHandler<UpdateCashMovementReasonCommand, Result<CashMovementReasonDto>>
{
    private readonly ICashMovementReasonRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public UpdateCashMovementReasonHandler(
        ICashMovementReasonRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CashMovementReasonDto>> Handle(
        UpdateCashMovementReasonCommand cmd,
        CancellationToken ct
    )
    {
        var reason = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (reason is null)
            return Result<CashMovementReasonDto>.NotFound("Motivo no encontrado.");

        var movementType = Enum.Parse<CashMovementType>(cmd.MovementType, true);
        reason.Update(cmd.Name, movementType, cmd.SortOrder, _u.UserId);
        await _repo.SaveChangesAsync(ct);

        return Result<CashMovementReasonDto>.Success(CajaMapper.ToDto(reason));
    }
}

public sealed class ToggleCashMovementReasonHandler
    : IRequestHandler<ToggleCashMovementReasonCommand, Result<CashMovementReasonDto>>
{
    private readonly ICashMovementReasonRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ToggleCashMovementReasonHandler(
        ICashMovementReasonRepository repo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<CashMovementReasonDto>> Handle(
        ToggleCashMovementReasonCommand cmd,
        CancellationToken ct
    )
    {
        var reason = await _repo.GetByIdAsync(_t.TenantId, _c.CompanyId, cmd.Id, ct);
        if (reason is null)
            return Result<CashMovementReasonDto>.NotFound("Motivo no encontrado.");

        if (reason.IsActive)
            reason.Disable(_u.UserId);
        else
            reason.Enable(_u.UserId);
        await _repo.SaveChangesAsync(ct);

        return Result<CashMovementReasonDto>.Success(CajaMapper.ToDto(reason));
    }
}

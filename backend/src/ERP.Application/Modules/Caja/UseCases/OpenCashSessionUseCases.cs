using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// El cliente solo envía CashRegisterId + OpeningAmount + Notes. BranchId, CompanyId, TenantId y
/// EmissionPointId se resuelven exclusivamente server-side (ICurrentBranch + CashRegister) —
/// ver ADR "Rediseño del módulo de Caja", Fase 2.
/// </summary>
public sealed record OpenCashSessionCommand(
    Guid CashRegisterId,
    decimal OpeningAmount,
    string? Notes = null
) : IRequest<Result<CashSessionDto>>, IBranchScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class OpenCashSessionValidator : AbstractValidator<OpenCashSessionCommand>
{
    public OpenCashSessionValidator()
    {
        RuleFor(x => x.CashRegisterId).NotEmpty().WithMessage("La caja es obligatoria.");
        RuleFor(x => x.OpeningAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El monto de apertura no puede ser negativo.");
        RuleFor(x => x.Notes)
            .MaximumLength(CashSession.NotesMaxLen)
            .WithMessage($"Las notas no pueden exceder {CashSession.NotesMaxLen} caracteres.");
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class OpenCashSessionHandler
    : IRequestHandler<OpenCashSessionCommand, Result<CashSessionDto>>
{
    private readonly ICashSessionRepository _repo;
    private readonly ICashRegisterRepository _cashRegisterRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public OpenCashSessionHandler(
        ICashSessionRepository repo,
        ICashRegisterRepository cashRegisterRepo,
        IEmissionPointRepository emissionPointRepo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _cashRegisterRepo = cashRegisterRepo;
        _emissionPointRepo = emissionPointRepo;
        _t = t;
        _b = b;
        _u = u;
        _dbEx = dbEx;
    }

    public async Task<Result<CashSessionDto>> Handle(
        OpenCashSessionCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        var existingByUser = await _repo.GetOpenByUserAsync(tid, uid, ct);
        if (existingByUser is not null)
            return Result<CashSessionDto>.ValidationFailure(
                "Ya tiene una caja abierta. Ciérrela antes de abrir otra."
            );

        var register = await _cashRegisterRepo.GetByIdAsync(tid, cmd.CashRegisterId, ct);
        if (register is null)
            return Result<CashSessionDto>.NotFound("Caja no encontrada.");
        if (!register.IsActive)
            return Result<CashSessionDto>.ValidationFailure("La caja está deshabilitada.");
        if (register.BranchId != _b.BranchId)
            return Result<CashSessionDto>.ValidationFailure(
                "La caja no pertenece a la sucursal activa."
            );
        if (register.EmissionPointId is null)
            return Result<CashSessionDto>.ValidationFailure(
                "La caja no tiene un punto de emisión configurado."
            );

        var existingByRegister = await _repo.GetOpenByCashRegisterAsync(tid, register.Id, ct);
        if (existingByRegister is not null)
            return Result<CashSessionDto>.ValidationFailure(
                "Ya existe una caja abierta en esta caja registradora."
            );

        var emissionPoint = await _emissionPointRepo.GetByIdAsync(
            register.EmissionPointId.Value,
            tid,
            ct
        );
        if (emissionPoint is null)
            return Result<CashSessionDto>.ValidationFailure(
                "El punto de emisión configurado en la caja ya no existe."
            );

        var session = CashSession.Open(
            tid,
            register.CompanyId,
            _b.BranchId,
            uid,
            register.Id,
            register.Code,
            register.Name,
            register.EmissionPointId.Value,
            emissionPoint.Code,
            cmd.OpeningAmount,
            uid,
            cmd.Notes
        );

        await _repo.AddAsync(session, ct);

        // Defensa final ante dos aperturas concurrentes para la misma caja/usuario: las
        // verificaciones previas (GetOpenByUserAsync/GetOpenByCashRegisterAsync) no son
        // suficientes bajo condición de carrera (dos requests pueden pasar ambas antes de que
        // cualquiera complete el INSERT). El índice único parcial de BD
        // (ux_cash_sessions_open_per_register / ux_cash_sessions_open_per_user,
        // CashSessionConfiguration) decide de forma atómica cuál gana; el "perdedor" recibe un
        // Result.Conflict controlado en vez de un 500 — mismo patrón que
        // CreateAuthenticatedSessionHandler/CreateItemCommandHandler vía IDatabaseExceptionTranslator.
        try
        {
            await _repo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<CashSessionDto>.Conflict(
                "Ya existe una sesión abierta para esta caja o para este usuario. Actualice e intente nuevamente."
            );
        }

        return Result<CashSessionDto>.Success(
            CajaMapper.ToDto(
                session,
                emissionPoint.EmissionType.ToString(),
                (register.DefaultWarehouseId, register.DefaultWarehouse?.Name),
                (register.DefaultCustomerId, register.DefaultCustomer?.Name.LegalName)
            )
        );
    }
}

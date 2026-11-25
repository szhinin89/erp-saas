using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Employees.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>Baja laboral (EMP-BR-08) con la versión de la ficha (D-E5-1). Motivo opcional, máximo 500.</summary>
public sealed record TerminateEmployeeCommand(Guid Id, uint RowVersion, DateOnly EndDate, string? Reason)
    : IRequest<Result<EmployeeDto>>,
        ICompanyScopedRequest;

/// <summary>Reincorporación (EMP-BR-09) con la versión de la ficha (D-E5-1).</summary>
public sealed record RehireEmployeeCommand(Guid Id, uint RowVersion, DateOnly StartDate)
    : IRequest<Result<EmployeeDto>>,
        ICompanyScopedRequest;

public sealed class TerminateEmployeeCommandValidator : AbstractValidator<TerminateEmployeeCommand>
{
    public TerminateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(EmployeeEmploymentPeriod.MaxTerminationReasonLength);
    }
}

public sealed class RehireEmployeeCommandValidator : AbstractValidator<RehireEmployeeCommand>
{
    public RehireEmployeeCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

// ── Handlers ────────────────────────────────────────────────────────────

/// <summary>
/// Baja: lock Employees → lecturas frescas (ficha, períodos, asignaciones) → existencia/pertenencia →
/// versión → reglas de negocio → cierre atómico de período y asignaciones. Lo rechazado no persiste ni audita.
/// </summary>
public sealed class TerminateEmployeeHandler
    : IRequestHandler<TerminateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public TerminateEmployeeHandler(
        IEmployeeRepository employees,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) => (_employees, _queries, _ctx, _unitOfWork) = (employees, queries, ctx, unitOfWork);

    public async Task<Result<EmployeeDto>> Handle(TerminateEmployeeCommand cmd, CancellationToken ct)
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var done = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.Id, ct);
                var employee = await _employees.GetWithPeriodsAsync(tenantId, companyId, cmd.Id, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<bool>();
                if (_employees.GetRowVersion(employee) != cmd.RowVersion)
                    return EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                var open = employee.OpenPeriod;
                if (open is null)
                    return Result<bool>.Failure(
                        "La ficha ya está dada de baja.",
                        ApiResponseCodes.Employees.AlreadyTerminated
                    );
                if (cmd.EndDate == default || cmd.EndDate < open.StartDate)
                    return Result<bool>.ValidationFailure(
                        "La fecha de baja es obligatoria y no puede ser anterior al inicio del período.",
                        ApiResponseCodes.Employees.InvalidDate
                    );

                var assignments = await _employees.GetAssignmentsAsync(tenantId, companyId, cmd.Id, ct);
                if (assignments.Any(a => a.StartDate > cmd.EndDate))
                    return Result<bool>.Failure(
                        "Existen asignaciones a sucursales posteriores a la fecha de baja.",
                        ApiResponseCodes.Employees.TerminationBlockedByFutureAssignments
                    );

                employee.Terminate(cmd.EndDate, cmd.Reason, assignments, _ctx.UserId);
                return await _employees.SaveChangesAsync(ct)
                    ? Result<bool>.Success(true)
                    : EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
            },
            ct
        );

        if (!done.IsSuccess)
            return Result<EmployeeDto>.Failure(done.Error!, done.Code);
        var dto = await _queries.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeDto>()
            : Result<EmployeeDto>.Success(dto);
    }
}

/// <summary>
/// Reincorporación: lock Employees → lock BpEmployment (orden global) → estado fresco de ficha y BP/rol →
/// existencia → versión → reglas → nuevo período. No reabre períodos ni asignaciones, no toca catálogos.
/// </summary>
public sealed class RehireEmployeeHandler
    : IRequestHandler<RehireEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeBusinessPartnerReader _bpReader;
    private readonly IBusinessPartnerEmploymentGuard _guard;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public RehireEmployeeHandler(
        IEmployeeRepository employees,
        IEmployeeBusinessPartnerReader bpReader,
        IBusinessPartnerEmploymentGuard guard,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) =>
        (_employees, _bpReader, _guard, _queries, _ctx, _unitOfWork) = (
            employees,
            bpReader,
            guard,
            queries,
            ctx,
            unitOfWork
        );

    public async Task<Result<EmployeeDto>> Handle(RehireEmployeeCommand cmd, CancellationToken ct)
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var done = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.Id, ct);
                var employee = await _employees.GetWithPeriodsAsync(tenantId, companyId, cmd.Id, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<bool>();
                if (_employees.GetRowVersion(employee) != cmd.RowVersion)
                    return EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                // Segundo lock del orden global Employees → BpEmployment; el estado del BP se lee después.
                await _guard.AcquireBpEmploymentLockAsync(tenantId, employee.BusinessPartnerId, ct);

                if (employee.OpenPeriod is not null)
                    return Result<bool>.Failure(
                        "La ficha ya está activa.",
                        ApiResponseCodes.Employees.AlreadyActive
                    );
                var bp = await _bpReader.GetEligibilityAsync(tenantId, employee.BusinessPartnerId, ct);
                if (bp is null)
                    return Result<bool>.NotFound("BusinessPartner no encontrado.");
                if (!bp.IsActive)
                    return Result<bool>.Failure(
                        "El tercero está inactivo.",
                        ApiResponseCodes.Employees.BpInactive
                    );
                if (!bp.HasActiveEmployeeRole)
                    return Result<bool>.Failure(
                        "El tercero no tiene el rol de Empleado activo.",
                        ApiResponseCodes.Employees.BpRoleMissing
                    );
                if (cmd.StartDate == default)
                    return Result<bool>.ValidationFailure(
                        "La fecha de reincorporación es obligatoria.",
                        ApiResponseCodes.Employees.InvalidDate
                    );
                if (employee.LastEndDate is { } last && cmd.StartDate <= last)
                    return Result<bool>.Failure(
                        "La reincorporación debe ser posterior a la fecha de baja anterior.",
                        ApiResponseCodes.Employees.PeriodOverlap
                    );

                var period = employee.Rehire(cmd.StartDate, _ctx.UserId);
                await _employees.AddPeriodAsync(period, ct);
                return await _employees.SaveChangesAsync(ct)
                    ? Result<bool>.Success(true)
                    : EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
            },
            ct
        );

        if (!done.IsSuccess)
            return Result<EmployeeDto>.Failure(done.Error!, done.Code);
        var dto = await _queries.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeDto>()
            : Result<EmployeeDto>.Success(dto);
    }
}

// ── Asignaciones a sucursales ───────────────────────────────────────────

public sealed record ListBranchAssignmentsQuery(Guid EmployeeId)
    : IRequest<Result<IReadOnlyList<EmployeeBranchAssignmentDto>>>,
        ICompanyScopedRequest;

public sealed record CreateBranchAssignmentCommand(
    Guid EmployeeId,
    Guid BranchId,
    DateOnly StartDate,
    DateOnly? EndDate
) : IRequest<Result<EmployeeBranchAssignmentDto>>, ICompanyScopedRequest;

/// <summary>Cierra una asignación abierta con la versión de la asignación (D-E5-1).</summary>
public sealed record CloseBranchAssignmentCommand(
    Guid EmployeeId,
    Guid AssignmentId,
    uint RowVersion,
    DateOnly EndDate
) : IRequest<Result<EmployeeBranchAssignmentDto>>, ICompanyScopedRequest;

public sealed class CreateBranchAssignmentCommandValidator
    : AbstractValidator<CreateBranchAssignmentCommand>
{
    public CreateBranchAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CloseBranchAssignmentCommandValidator
    : AbstractValidator<CloseBranchAssignmentCommand>
{
    public CloseBranchAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}

internal static class BranchAssignmentRules
{
    /// <summary>
    /// INV-08: el rango [start, end] (fin nulo = abierto) debe quedar completamente dentro de un período
    /// laboral. Una asignación abierta exige un período abierto.
    /// </summary>
    public static bool IsWithinAPeriod(Employee employee, DateOnly start, DateOnly? end) =>
        employee.Periods.Any(p =>
            p.StartDate <= start
            && (p.EndDate is null || (end is not null && end <= p.EndDate && start <= p.EndDate))
        );
}

public sealed class ListBranchAssignmentsHandler
    : IRequestHandler<ListBranchAssignmentsQuery, Result<IReadOnlyList<EmployeeBranchAssignmentDto>>>
{
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;

    public ListBranchAssignmentsHandler(IEmployeeQueries queries, IOperationalContext ctx) =>
        (_queries, _ctx) = (queries, ctx);

    public async Task<Result<IReadOnlyList<EmployeeBranchAssignmentDto>>> Handle(
        ListBranchAssignmentsQuery q,
        CancellationToken ct
    )
    {
        var list = await _queries.ListAssignmentsAsync(_ctx.TenantId, _ctx.CompanyId, q.EmployeeId, ct);
        return list is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<IReadOnlyList<EmployeeBranchAssignmentDto>>()
            : Result<IReadOnlyList<EmployeeBranchAssignmentDto>>.Success(list);
    }
}

/// <summary>
/// Crear asignación: lock Employees → ficha, períodos y asignaciones frescos → ficha activa → sucursal activa
/// del ámbito (FOR SHARE) → fechas → contención en período (INV-08) → sin solapamiento (INV-07).
/// </summary>
public sealed class CreateBranchAssignmentHandler
    : IRequestHandler<CreateBranchAssignmentCommand, Result<EmployeeBranchAssignmentDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeBranchReader _branches;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBranchAssignmentHandler(
        IEmployeeRepository employees,
        IEmployeeBranchReader branches,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) =>
        (_employees, _branches, _queries, _ctx, _unitOfWork) = (employees, branches, queries, ctx, unitOfWork);

    public async Task<Result<EmployeeBranchAssignmentDto>> Handle(
        CreateBranchAssignmentCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var created = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.EmployeeId, ct);
                var employee = await _employees.GetWithPeriodsAsync(tenantId, companyId, cmd.EmployeeId, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<Guid>();
                if (employee.OpenPeriod is null)
                    return Result<Guid>.Failure(
                        "La ficha está dada de baja: no admite nuevas asignaciones.",
                        ApiResponseCodes.Employees.AlreadyTerminated
                    );

                if (!await _branches.TryLockActiveAsync(tenantId, companyId, cmd.BranchId, ct))
                    return Result<Guid>.ValidationFailure(
                        "La sucursal indicada no es válida.",
                        ApiResponseCodes.Employees.BranchInvalid
                    );

                if (cmd.StartDate == default || (cmd.EndDate is { } end && end < cmd.StartDate))
                    return Result<Guid>.ValidationFailure(
                        "Las fechas de la asignación no son válidas.",
                        ApiResponseCodes.Employees.InvalidDate
                    );
                if (!BranchAssignmentRules.IsWithinAPeriod(employee, cmd.StartDate, cmd.EndDate))
                    return Result<Guid>.Failure(
                        "Las fechas de la asignación deben estar dentro de un período laboral del empleado.",
                        ApiResponseCodes.Employees.AssignmentOutsidePeriod
                    );

                var assignments = await _employees.GetAssignmentsAsync(tenantId, companyId, cmd.EmployeeId, ct);
                if (assignments.Any(a => a.BranchId == cmd.BranchId && a.Overlaps(cmd.StartDate, cmd.EndDate)))
                    return Result<Guid>.Failure(
                        "La asignación se solapa con otra del empleado en la misma sucursal.",
                        ApiResponseCodes.Employees.AssignmentOverlap
                    );

                var assignment = EmployeeBranchAssignment.Create(
                    tenantId, companyId, employee.Id, cmd.BranchId, cmd.StartDate, cmd.EndDate, _ctx.UserId);
                await _employees.AddAssignmentAsync(assignment, ct);
                employee.RecordAssignmentCreated(assignment);
                return await _employees.SaveChangesAsync(ct)
                    ? Result<Guid>.Success(assignment.Id)
                    : EmployeeUseCaseSupport.ConcurrencyConflict<Guid>();
            },
            ct
        );

        if (!created.IsSuccess)
            return Result<EmployeeBranchAssignmentDto>.Failure(created.Error!, created.Code);
        var dto = await _queries.GetAssignmentAsync(tenantId, companyId, cmd.EmployeeId, created.Value, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeBranchAssignmentDto>()
            : Result<EmployeeBranchAssignmentDto>.Success(dto);
    }
}

/// <summary>
/// Cerrar asignación: lock Employees → existencia y pertenencia (ficha y asignación) → versión → reglas.
/// Una asignación ya cerrada es una violación de regla de dominio (D-E5-5).
/// </summary>
public sealed class CloseBranchAssignmentHandler
    : IRequestHandler<CloseBranchAssignmentCommand, Result<EmployeeBranchAssignmentDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public CloseBranchAssignmentHandler(
        IEmployeeRepository employees,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) => (_employees, _queries, _ctx, _unitOfWork) = (employees, queries, ctx, unitOfWork);

    public async Task<Result<EmployeeBranchAssignmentDto>> Handle(
        CloseBranchAssignmentCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var done = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.EmployeeId, ct);
                var employee = await _employees.GetWithPeriodsAsync(tenantId, companyId, cmd.EmployeeId, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<bool>();
                var assignment = await _employees.GetAssignmentAsync(
                    tenantId, companyId, cmd.EmployeeId, cmd.AssignmentId, ct);
                if (assignment is null)
                    return Result<bool>.NotFound("La asignación no existe.");
                if (_employees.GetAssignmentRowVersion(assignment) != cmd.RowVersion)
                    return EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                _employees.SetExpectedAssignmentRowVersion(assignment, cmd.RowVersion);

                if (assignment.EndDate is not null)
                    throw new DomainRuleViolationException("La asignación ya está cerrada.");
                if (cmd.EndDate == default || cmd.EndDate < assignment.StartDate)
                    return Result<bool>.ValidationFailure(
                        "La fecha de cierre no es válida.",
                        ApiResponseCodes.Employees.InvalidDate
                    );
                if (!BranchAssignmentRules.IsWithinAPeriod(employee, assignment.StartDate, cmd.EndDate))
                    return Result<bool>.Failure(
                        "La fecha de cierre debe estar dentro del período laboral de la asignación.",
                        ApiResponseCodes.Employees.AssignmentOutsidePeriod
                    );

                assignment.Close(cmd.EndDate, _ctx.UserId);
                employee.RecordAssignmentClosed(assignment, null);
                return await _employees.SaveChangesAsync(ct)
                    ? Result<bool>.Success(true)
                    : EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
            },
            ct
        );

        if (!done.IsSuccess)
            return Result<EmployeeBranchAssignmentDto>.Failure(done.Error!, done.Code);
        var dto = await _queries.GetAssignmentAsync(tenantId, companyId, cmd.EmployeeId, cmd.AssignmentId, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeBranchAssignmentDto>()
            : Result<EmployeeBranchAssignmentDto>.Success(dto);
    }
}

using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Employees.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// Alta de la ficha y su primer período (EMP-BR-01/02/07). Tenant y empresa salen del contexto
/// autenticado; nunca del cuerpo.
/// </summary>
public sealed record CreateEmployeeCommand(
    Guid BusinessPartnerId,
    DateOnly StartDate,
    Guid? JobPositionId = null,
    Guid? ProfessionId = null,
    IReadOnlyList<Guid>? SpecialtyIds = null
) : IRequest<Result<EmployeeDto>>, ICompanyScopedRequest;

/// <summary>Edita exclusivamente cargo y profesión; distingue omitido de null explícito (D-3).</summary>
public sealed record UpdateEmployeeCommand(
    Guid Id,
    uint RowVersion,
    OptionalField<Guid?> JobPositionId = default,
    OptionalField<Guid?> ProfessionId = default
) : IRequest<Result<EmployeeDto>>, ICompanyScopedRequest;

/// <summary>Fija el conjunto de especialidades activas de la ficha (EMP-BR-06).</summary>
public sealed record SetEmployeeSpecialtiesCommand(
    Guid Id,
    uint RowVersion,
    IReadOnlyList<Guid> SpecialtyIds
) : IRequest<Result<EmployeeDto>>, ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        // StartDate: la ausencia o una fecha por defecto la rechaza el handler con
        // EMPLOYEE_INVALID_DATE (D-1: sin otras cotas).
        RuleFor(x => x.JobPositionId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProfessionId).NotEqual(Guid.Empty);
        RuleForEach(x => x.SpecialtyIds).NotEmpty();
    }
}

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.JobPositionId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.ProfessionId.Value).NotEqual(Guid.Empty);
    }
}

public sealed class SetEmployeeSpecialtiesCommandValidator
    : AbstractValidator<SetEmployeeSpecialtiesCommand>
{
    public SetEmployeeSpecialtiesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SpecialtyIds).NotNull();
        RuleForEach(x => x.SpecialtyIds).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateEmployeeHandler
    : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IEmployeeBusinessPartnerReader _bpReader;
    private readonly IBusinessPartnerEmploymentGuard _guard;
    private readonly IIdentificationUsageValidator _identification;
    private readonly IEmployeeQueries _queries;
    private readonly IDatabaseExceptionTranslator _translator;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeHandler(
        IEmployeeRepository employees,
        IEmployeeCatalogRepository catalogs,
        IEmployeeBusinessPartnerReader bpReader,
        IBusinessPartnerEmploymentGuard guard,
        IIdentificationUsageValidator identification,
        IEmployeeQueries queries,
        IDatabaseExceptionTranslator translator,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    )
    {
        _employees = employees;
        _catalogs = catalogs;
        _bpReader = bpReader;
        _guard = guard;
        _identification = identification;
        _queries = queries;
        _translator = translator;
        _ctx = ctx;
        _unitOfWork = unitOfWork;
    }

    private static IReadOnlyCollection<Guid> One(Guid? id) =>
        id is { } value ? new[] { value } : Array.Empty<Guid>();

    public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand cmd, CancellationToken ct)
    {
        if (cmd.StartDate == default)
            return Result<EmployeeDto>.ValidationFailure(
                "La fecha de ingreso es obligatoria y debe ser válida.",
                ApiResponseCodes.Employees.InvalidDate
            );

        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;
        var specialtyIds = (cmd.SpecialtyIds ?? Array.Empty<Guid>()).Distinct().ToList();

        var created = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                // Protocolo EMP-E4/T-05: lock BpEmployment → lecturas frescas → escritura. Cada
                // lectura ocurre DESPUÉS del lock: una desactivación o revocación ya confirmada se
                // ve; si no, esa operación espera a que esta transacción termine.
                await _guard.AcquireBpEmploymentLockAsync(tenantId, cmd.BusinessPartnerId, ct);

                var bp = await _bpReader.GetEligibilityAsync(tenantId, cmd.BusinessPartnerId, ct);
                if (bp is null)
                    return Result<Guid>.NotFound("BusinessPartner no encontrado.");
                if (!bp.IsActive)
                    return Result<Guid>.Failure(
                        "El tercero está inactivo.",
                        ApiResponseCodes.Employees.BpInactive
                    );
                if (!bp.HasActiveEmployeeRole)
                    return Result<Guid>.Failure(
                        "El tercero no tiene el rol de Empleado activo.",
                        ApiResponseCodes.Employees.BpRoleMissing
                    );
                if (
                    !await _identification.IsAllowedAsync(
                        bp.IdentificationType,
                        IdentificationUsageType.Employee,
                        ct
                    )
                )
                    return Result<Guid>.Failure(
                        "El tipo de identificación del tercero no está permitido para empleados.",
                        ApiResponseCodes.Employees.IdentificationNotAllowed
                    );
                if (
                    await _employees.ExistsForBusinessPartnerAsync(
                        tenantId,
                        companyId,
                        bp.BusinessPartnerId,
                        ct
                    )
                )
                    return Result<Guid>.Failure(
                        "El tercero ya tiene una ficha de empleado en esta empresa.",
                        ApiResponseCodes.Employees.AlreadyExists
                    );

                var invalid =
                    await EmployeeUseCaseSupport.ValidateAssignableAsync<Guid>(
                        _catalogs,
                        EmployeeCatalogType.JobPosition,
                        "El cargo",
                        tenantId,
                        companyId,
                        One(cmd.JobPositionId),
                        ct
                    )
                    ?? await EmployeeUseCaseSupport.ValidateAssignableAsync<Guid>(
                        _catalogs,
                        EmployeeCatalogType.Profession,
                        "La profesión",
                        tenantId,
                        companyId,
                        One(cmd.ProfessionId),
                        ct
                    )
                    ?? await EmployeeUseCaseSupport.ValidateAssignableAsync<Guid>(
                        _catalogs,
                        EmployeeCatalogType.Specialty,
                        "La especialidad",
                        tenantId,
                        companyId,
                        specialtyIds,
                        ct
                    );
                if (invalid is not null)
                    return invalid;

                var employee = Employee.Create(
                    tenantId,
                    companyId,
                    bp.BusinessPartnerId,
                    cmd.StartDate,
                    _ctx.UserId,
                    cmd.JobPositionId,
                    cmd.ProfessionId
                );
                await _employees.AddAsync(employee, ct);
                foreach (var specialtyId in specialtyIds)
                {
                    await _employees.AddSpecialtyLinkAsync(
                        EmployeeSpecialty.Create(
                            tenantId,
                            companyId,
                            employee.Id,
                            specialtyId,
                            _ctx.UserId
                        ),
                        ct
                    );
                    employee.RecordSpecialtyChange(specialtyId, assigned: true, _ctx.UserId);
                }

                try
                {
                    if (!await _employees.SaveChangesAsync(ct))
                        return EmployeeUseCaseSupport.ConcurrencyConflict<Guid>();
                }
                catch (Exception ex) when (_translator.TryGetUniqueViolation(ex, out _))
                {
                    return Result<Guid>.Failure(
                        "El tercero ya tiene una ficha de empleado en esta empresa.",
                        ApiResponseCodes.Employees.AlreadyExists
                    );
                }
                return Result<Guid>.Success(employee.Id);
            },
            ct
        );

        if (!created.IsSuccess)
            return Result<EmployeeDto>.Failure(created.Error!, created.Code);

        var dto = await _queries.GetByIdAsync(tenantId, companyId, created.Value, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeDto>()
            : Result<EmployeeDto>.Success(dto);
    }
}

public sealed class UpdateEmployeeHandler
    : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeHandler(
        IEmployeeRepository employees,
        IEmployeeCatalogRepository catalogs,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) =>
        (_employees, _catalogs, _queries, _ctx, _unitOfWork) = (
            employees,
            catalogs,
            queries,
            ctx,
            unitOfWork
        );

    public async Task<Result<EmployeeDto>> Handle(UpdateEmployeeCommand cmd, CancellationToken ct)
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;

        var done = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.Id, ct);
                var employee = await _employees.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<bool>();
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                // D-2: conservar un valor inactivo ya asignado es válido; solo un cambio exige activo.
                Result<bool>? invalid = null;
                if (
                    cmd.JobPositionId.IsSet
                    && cmd.JobPositionId.Value is { } job
                    && job != employee.JobPositionId
                )
                    invalid = await EmployeeUseCaseSupport.ValidateAssignableAsync<bool>(
                        _catalogs,
                        EmployeeCatalogType.JobPosition,
                        "El cargo",
                        tenantId,
                        companyId,
                        new[] { job },
                        ct
                    );
                if (
                    invalid is null
                    && cmd.ProfessionId.IsSet
                    && cmd.ProfessionId.Value is { } profession
                    && profession != employee.ProfessionId
                )
                    invalid = await EmployeeUseCaseSupport.ValidateAssignableAsync<bool>(
                        _catalogs,
                        EmployeeCatalogType.Profession,
                        "La profesión",
                        tenantId,
                        companyId,
                        new[] { profession },
                        ct
                    );
                if (invalid is not null)
                    return invalid;

                if (cmd.JobPositionId.IsSet)
                    employee.ChangeJobPosition(cmd.JobPositionId.Value, _ctx.UserId);
                if (cmd.ProfessionId.IsSet)
                    employee.ChangeProfession(cmd.ProfessionId.Value, _ctx.UserId);
                employee.Touch(_ctx.UserId); // fuerza la comprobación de xmin aunque nada cambie

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

public sealed class SetEmployeeSpecialtiesHandler
    : IRequestHandler<SetEmployeeSpecialtiesCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeCatalogRepository _catalogs;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public SetEmployeeSpecialtiesHandler(
        IEmployeeRepository employees,
        IEmployeeCatalogRepository catalogs,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) =>
        (_employees, _catalogs, _queries, _ctx, _unitOfWork) = (
            employees,
            catalogs,
            queries,
            ctx,
            unitOfWork
        );

    public async Task<Result<EmployeeDto>> Handle(
        SetEmployeeSpecialtiesCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;
        var desired = cmd.SpecialtyIds.Distinct().ToHashSet();

        var done = await EmployeeUseCaseSupport.RunInTransactionAsync(
            _unitOfWork,
            async () =>
            {
                await _employees.AcquireEmployeeLockAsync(cmd.Id, ct);
                var employee = await _employees.GetByIdAsync(tenantId, companyId, cmd.Id, ct);
                if (employee is null)
                    return EmployeeUseCaseSupport.EmployeeNotFound<bool>();
                // El token de la ficha principal también protege este cambio (xmin de employees).
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                var links = (
                    await _employees.GetSpecialtyLinksAsync(tenantId, companyId, cmd.Id, ct)
                ).ToDictionary(l => l.SpecialtyId);

                // D-2: las especialidades ya activas se conservan sin revalidar; las nuevas o
                // reasignadas deben existir en el ámbito y estar activas.
                var toAssign = desired
                    .Where(id => !links.TryGetValue(id, out var l) || !l.IsActive)
                    .ToList();
                var invalid = await EmployeeUseCaseSupport.ValidateAssignableAsync<bool>(
                    _catalogs,
                    EmployeeCatalogType.Specialty,
                    "La especialidad",
                    tenantId,
                    companyId,
                    toAssign,
                    ct
                );
                if (invalid is not null)
                    return invalid;

                foreach (var id in toAssign)
                {
                    if (links.TryGetValue(id, out var existing))
                        existing.Activate(_ctx.UserId);
                    else
                        await _employees.AddSpecialtyLinkAsync(
                            EmployeeSpecialty.Create(
                                tenantId,
                                companyId,
                                employee.Id,
                                id,
                                _ctx.UserId
                            ),
                            ct
                        );
                    employee.RecordSpecialtyChange(id, assigned: true, _ctx.UserId);
                }
                foreach (
                    var (id, link) in links.Where(kv => kv.Value.IsActive && !desired.Contains(kv.Key))
                )
                {
                    link.Deactivate(_ctx.UserId);
                    employee.RecordSpecialtyChange(id, assigned: false, _ctx.UserId);
                }
                employee.Touch(_ctx.UserId);

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

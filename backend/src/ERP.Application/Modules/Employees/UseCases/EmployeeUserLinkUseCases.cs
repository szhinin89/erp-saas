using ERP.Application.Access.UseCases;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Employees.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Employees.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>Vincula un usuario ERP existente a la ficha (EMP-BR-13). No crea cuentas, membresías ni permisos.</summary>
public sealed record LinkEmployeeUserCommand(Guid Id, Guid IdentityUserId, uint RowVersion)
    : IRequest<Result<EmployeeDto>>,
        ICompanyScopedRequest;

/// <summary>Quita el vínculo (EMP-BR-14). No toca la cuenta, la membresía ni los permisos.</summary>
public sealed record UnlinkEmployeeUserCommand(Guid Id, uint RowVersion)
    : IRequest<Result<EmployeeDto>>,
        ICompanyScopedRequest;

public sealed class LinkEmployeeUserCommandValidator : AbstractValidator<LinkEmployeeUserCommand>
{
    public LinkEmployeeUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.IdentityUserId).NotEmpty();
    }
}

public sealed class UnlinkEmployeeUserCommandValidator : AbstractValidator<UnlinkEmployeeUserCommand>
{
    public UnlinkEmployeeUserCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

// ── Proyección ──────────────────────────────────────────────────────────

/// <summary>
/// Proyección mínima del usuario vinculado con la regla única de IAM (IAM-V1-F1): solo si la identidad
/// tiene membresía en la empresa de la ficha y no es global; en otro caso <c>null</c>.
/// </summary>
public sealed class EmployeeIdentityProjector : IEmployeeIdentityProjector
{
    private readonly IAccessRepository _access;

    public EmployeeIdentityProjector(IAccessRepository access) => _access = access;

    public async Task<EmployeeIdentityUserDto?> ProjectAsync(
        Guid companyId,
        Guid identityUserId,
        CancellationToken ct = default
    )
    {
        var membership = await IamIdentityScope.GetAdministrableMembershipAsync(
            _access,
            companyId,
            identityUserId,
            ct
        );
        if (membership is null)
            return null;
        var user = await _access.GetUserByIdAsync(identityUserId, ct);
        return user is null
            ? null
            : new EmployeeIdentityUserDto(user.Id, user.Username, membership.IsActive);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

/// <summary>
/// Vincular: lock Employees → estado fresco → existencia/pertenencia → versión → ficha sin vínculo →
/// membresía bloqueada FOR SHARE, activa y no global → usuario libre en la empresa → registro y auditoría.
/// </summary>
public sealed class LinkEmployeeUserHandler
    : IRequestHandler<LinkEmployeeUserCommand, Result<EmployeeDto>>
{
    private const string UniqueLinkConstraint = "uq_employees_company_identity_user";

    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeUserReader _users;
    private readonly IAccessRepository _access;
    private readonly IEmployeeQueries _queries;
    private readonly IDatabaseExceptionTranslator _translator;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public LinkEmployeeUserHandler(
        IEmployeeRepository employees,
        IEmployeeUserReader users,
        IAccessRepository access,
        IEmployeeQueries queries,
        IDatabaseExceptionTranslator translator,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    )
    {
        _employees = employees;
        _users = users;
        _access = access;
        _queries = queries;
        _translator = translator;
        _ctx = ctx;
        _unitOfWork = unitOfWork;
    }

    // Mismo mensaje para inexistente, sin membresía, de otra empresa o tenant, inactiva o cuenta global.
    private static Result<bool> NoMembership() =>
        Result<bool>.Failure(
            "El usuario no tiene una membresía activa en esta empresa.",
            ApiResponseCodes.Employees.UserNoMembership
        );

    private static Result<bool> AlreadyLinked() =>
        Result<bool>.Failure(
            "El usuario o la ficha ya tienen un vínculo. Desvincule primero.",
            ApiResponseCodes.Employees.UserAlreadyLinked
        );

    public async Task<Result<EmployeeDto>> Handle(LinkEmployeeUserCommand cmd, CancellationToken ct)
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
                if (_employees.GetRowVersion(employee) != cmd.RowVersion)
                    return EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                // D-E6-2: una ficha vinculada no se reemplaza, ni con el mismo usuario.
                if (employee.IdentityUserId is not null)
                    return AlreadyLinked();

                if (!await _users.TryLockMembershipAsync(tenantId, companyId, cmd.IdentityUserId, ct))
                    return NoMembership();
                // Lectura posterior al bloqueo: regla única de IAM (membresía en la empresa y no global).
                var membership = await IamIdentityScope.GetAdministrableMembershipAsync(
                    _access,
                    companyId,
                    cmd.IdentityUserId,
                    ct
                );
                if (membership is not { IsActive: true })
                    return NoMembership();

                // Solo tras acreditar la elegibilidad: no revela nada de usuarios fuera de alcance.
                if (
                    await _employees.IsUserLinkedToOtherEmployeeAsync(
                        tenantId,
                        companyId,
                        cmd.IdentityUserId,
                        employee.Id,
                        ct
                    )
                )
                    return AlreadyLinked();

                employee.LinkUser(cmd.IdentityUserId, _ctx.UserId);
                try
                {
                    return await _employees.SaveChangesAsync(ct)
                        ? Result<bool>.Success(true)
                        : EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                }
                // UX-EMP-02: solo esa restricción se traduce; cualquier otro error se propaga.
                catch (Exception ex)
                    when (_translator.TryGetUniqueViolation(ex, out var violation)
                        && violation.ConstraintName == UniqueLinkConstraint
                    )
                {
                    return AlreadyLinked();
                }
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
/// Desvincular: lock Employees → existencia/pertenencia → versión → vínculo existente → null y auditoría.
/// No exige membresía activa ni mira la cuenta: una cuenta global también se puede desvincular.
/// </summary>
public sealed class UnlinkEmployeeUserHandler
    : IRequestHandler<UnlinkEmployeeUserCommand, Result<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;

    public UnlinkEmployeeUserHandler(
        IEmployeeRepository employees,
        IEmployeeQueries queries,
        IOperationalContext ctx,
        IUnitOfWork unitOfWork
    ) => (_employees, _queries, _ctx, _unitOfWork) = (employees, queries, ctx, unitOfWork);

    public async Task<Result<EmployeeDto>> Handle(UnlinkEmployeeUserCommand cmd, CancellationToken ct)
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
                if (_employees.GetRowVersion(employee) != cmd.RowVersion)
                    return EmployeeUseCaseSupport.ConcurrencyConflict<bool>();
                _employees.SetExpectedRowVersion(employee, cmd.RowVersion);

                if (employee.IdentityUserId is null)
                    return Result<bool>.Failure(
                        "La ficha no tiene un usuario vinculado.",
                        ApiResponseCodes.Employees.UserNotLinked
                    );

                employee.UnlinkUser(_ctx.UserId);
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

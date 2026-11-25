using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Employees.UseCases;

public sealed record GetEmployeeByIdQuery(Guid Id)
    : IRequest<Result<EmployeeDto>>,
        ICompanyScopedRequest;

/// <summary>Lista paginada con skip/take (E1, decisión 4). El filtro por sucursal llega con EMP-E5 (D-4).</summary>
public sealed record ListEmployeesQuery(
    bool? IsActive = null,
    string? Search = null,
    Guid? JobPositionId = null,
    Guid? ProfessionId = null,
    Guid? SpecialtyId = null,
    Guid? BranchId = null,
    DateOnly? AsOf = null,
    int Skip = 0,
    int Take = 50
) : IRequest<Result<PagedResult<EmployeeListItemDto>>>, ICompanyScopedRequest;

public sealed class ListEmployeesQueryValidator : AbstractValidator<ListEmployeesQuery>
{
    public ListEmployeesQueryValidator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 200);
        RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.AsOf).Must(d => d is null || d != default).WithMessage("La fecha de referencia no es válida.");
    }
}

public sealed class GetEmployeeByIdHandler
    : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDto>>
{
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;

    public GetEmployeeByIdHandler(IEmployeeQueries queries, IOperationalContext ctx) =>
        (_queries, _ctx) = (queries, ctx);

    public async Task<Result<EmployeeDto>> Handle(GetEmployeeByIdQuery q, CancellationToken ct)
    {
        var dto = await _queries.GetByIdAsync(_ctx.TenantId, _ctx.CompanyId, q.Id, ct);
        return dto is null
            ? EmployeeUseCaseSupport.EmployeeNotFound<EmployeeDto>()
            : Result<EmployeeDto>.Success(dto);
    }
}

public sealed class ListEmployeesHandler
    : IRequestHandler<ListEmployeesQuery, Result<PagedResult<EmployeeListItemDto>>>
{
    private readonly IEmployeeQueries _queries;
    private readonly IOperationalContext _ctx;
    private readonly ICompanyClock _clock;

    public ListEmployeesHandler(IEmployeeQueries queries, IOperationalContext ctx, ICompanyClock clock) =>
        (_queries, _ctx, _clock) = (queries, ctx, clock);

    public async Task<Result<PagedResult<EmployeeListItemDto>>> Handle(
        ListEmployeesQuery q,
        CancellationToken ct
    )
    {
        var take = Math.Clamp(q.Take, 1, 200);
        var skip = Math.Max(q.Skip, 0);
        // D-E5-4: fecha de referencia = hoy de la empresa (ICompanyClock), nunca la fecha UTC del servidor.
        DateOnly? asOf = q.BranchId is null
            ? null
            : q.AsOf ?? await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        var (items, total) = await _queries.ListAsync(
            _ctx.TenantId,
            _ctx.CompanyId,
            new EmployeeListFilter(
                q.IsActive, q.Search, q.JobPositionId, q.ProfessionId, q.SpecialtyId, q.BranchId, asOf),
            skip,
            take,
            ct
        );
        return Result<PagedResult<EmployeeListItemDto>>.Success(
            new PagedResult<EmployeeListItemDto>(items, (skip / take) + 1, take, total)
        );
    }
}

using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

// CUSTOMER-ADVANCES-V1 CA-F3 — consultas de solo lectura, acotadas a tenant y empresa activa. La
// sucursal es un filtro opcional del listado: nunca limita el detalle ni el saldo (CA-INV-05).

public sealed record GetCustomerAdvanceListQuery(
    int Page = 1,
    int PageSize = 25,
    Guid? CustomerId = null,
    CustomerAdvanceStatus? Status = null,
    Guid? BranchId = null
) : IRequest<Result<CustomerAdvanceListResultDto>>, ICompanyScopedRequest;

public sealed record GetCustomerAdvanceByIdQuery(Guid Id)
    : IRequest<Result<CustomerAdvanceDto>>,
        ICompanyScopedRequest;

public sealed record GetCustomerAdvanceBalanceQuery(Guid CustomerId)
    : IRequest<Result<CustomerAdvanceBalanceDto>>,
        ICompanyScopedRequest;

public sealed class GetCustomerAdvanceListQueryValidator
    : AbstractValidator<GetCustomerAdvanceListQuery>
{
    public GetCustomerAdvanceListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class GetCustomerAdvanceBalanceQueryValidator
    : AbstractValidator<GetCustomerAdvanceBalanceQuery>
{
    public GetCustomerAdvanceBalanceQueryValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public sealed class GetCustomerAdvanceListHandler(
    ICustomerAdvanceRepository advances,
    IBusinessPartnerRepository partners,
    ICurrentTenant tenant
) : IRequestHandler<GetCustomerAdvanceListQuery, Result<CustomerAdvanceListResultDto>>
{
    public async Task<Result<CustomerAdvanceListResultDto>> Handle(
        GetCustomerAdvanceListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await advances.SearchAsync(
            tenant.TenantId,
            new CustomerAdvanceSearchCriteria(q.CustomerId, q.Status, q.BranchId),
            q.Page,
            q.PageSize,
            ct
        );
        var names = await partners.GetNamesByIdsAsync(
            items.Select(a => a.CustomerId).Distinct(),
            ct
        );

        return Result<CustomerAdvanceListResultDto>.Success(
            new CustomerAdvanceListResultDto(
                items
                    .Select(a =>
                        CustomerAdvanceMap.ToListItem(a, names.GetValueOrDefault(a.CustomerId))
                    )
                    .ToList(),
                total,
                q.Page,
                q.PageSize
            )
        );
    }
}

public sealed class GetCustomerAdvanceByIdHandler(
    ICustomerAdvanceRepository advances,
    IBusinessPartnerRepository partners,
    ICurrentTenant tenant
) : IRequestHandler<GetCustomerAdvanceByIdQuery, Result<CustomerAdvanceDto>>
{
    public async Task<Result<CustomerAdvanceDto>> Handle(
        GetCustomerAdvanceByIdQuery q,
        CancellationToken ct
    )
    {
        var advance = await advances.GetByIdAsync(tenant.TenantId, q.Id, ct);
        if (advance is null)
            return Result<CustomerAdvanceDto>.NotFound("El anticipo indicado no existe.");
        var names = await partners.GetNamesByIdsAsync([advance.CustomerId], ct);

        return Result<CustomerAdvanceDto>.Success(
            CustomerAdvanceMap.ToDto(advance, names.GetValueOrDefault(advance.CustomerId))
        );
    }
}

public sealed class GetCustomerAdvanceBalanceHandler(
    ICustomerAdvanceRepository advances,
    IBusinessPartnerRepository partners,
    ICompanyRepository companies,
    ICurrentTenant tenant,
    ICurrentCompany company
) : IRequestHandler<GetCustomerAdvanceBalanceQuery, Result<CustomerAdvanceBalanceDto>>
{
    public async Task<Result<CustomerAdvanceBalanceDto>> Handle(
        GetCustomerAdvanceBalanceQuery q,
        CancellationToken ct
    )
    {
        var customer = await partners.GetByIdAsync(q.CustomerId, ct);
        var activeCompany = await companies.GetByIdAsync(company.CompanyId, ct);
        if (customer is null || customer.TenantId != tenant.TenantId || activeCompany is null)
            return Result<CustomerAdvanceBalanceDto>.NotFound("Cliente no encontrado.");

        // Suma de los disponibles persistidos por el libro: no hay otro saldo.
        var available = await advances.GetAvailableBalanceAsync(tenant.TenantId, customer.Id, ct);

        return Result<CustomerAdvanceBalanceDto>.Success(
            new CustomerAdvanceBalanceDto(customer.Id, activeCompany.CurrencyCode, available)
        );
    }
}

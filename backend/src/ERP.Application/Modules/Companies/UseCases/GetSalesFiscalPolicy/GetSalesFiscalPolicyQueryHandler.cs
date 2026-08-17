using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetSalesFiscalPolicy;

public sealed class GetSalesFiscalPolicyQueryHandler
    : IRequestHandler<GetSalesFiscalPolicyQuery, Result<SalesFiscalPolicyDto>>
{
    private readonly ISalesFiscalPolicyResolver _resolver;

    public GetSalesFiscalPolicyQueryHandler(ISalesFiscalPolicyResolver resolver) =>
        _resolver = resolver;

    public async Task<Result<SalesFiscalPolicyDto>> Handle(
        GetSalesFiscalPolicyQuery request,
        CancellationToken cancellationToken
    )
    {
        var policy = await _resolver.GetEffectivePolicyAsync(cancellationToken);
        return Result<SalesFiscalPolicyDto>.Success(SalesFiscalPolicyMapper.ToDto(policy));
    }
}

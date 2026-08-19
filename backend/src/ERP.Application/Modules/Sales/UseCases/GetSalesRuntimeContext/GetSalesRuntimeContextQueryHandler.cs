using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesRuntimeContext;

public sealed class GetSalesRuntimeContextQueryHandler
    : IRequestHandler<GetSalesRuntimeContextQuery, Result<SalesRuntimeContextDto>>
{
    private readonly ISalesFiscalPolicyResolver _fiscalPolicyResolver;
    private readonly IMediator _mediator;

    public GetSalesRuntimeContextQueryHandler(
        ISalesFiscalPolicyResolver fiscalPolicyResolver,
        IMediator mediator
    )
    {
        _fiscalPolicyResolver = fiscalPolicyResolver;
        _mediator = mediator;
    }

    public async Task<Result<SalesRuntimeContextDto>> Handle(
        GetSalesRuntimeContextQuery request,
        CancellationToken cancellationToken
    )
    {
        var policy = await _fiscalPolicyResolver.GetEffectivePolicyAsync(cancellationToken);

        // Reutiliza GetSalesInvoiceDefaultsQuery (única fuente de verdad de defaults de
        // factura) en vez de reimplementar la resolución de OrgSettings/EmissionPoint aquí.
        var defaultsResult = await _mediator.Send(
            new GetSalesInvoiceDefaultsQuery(),
            cancellationToken
        );
        if (!defaultsResult.IsSuccess)
            return Result<SalesRuntimeContextDto>.Failure(defaultsResult.Error!);

        var d = defaultsResult.Value!;

        return Result<SalesRuntimeContextDto>.Success(
            new SalesRuntimeContextDto(
                SalesFiscalPolicyMapper.ToDto(policy),
                new SalesRuntimeDefaultsDto(
                    d.DefaultDocTypeCode,
                    d.DefaultSriPaymentMethodCode,
                    d.DefaultEmissionPointId,
                    d.DefaultWarehouseId,
                    d.DefaultPaymentTermId,
                    d.FallbackDocTypeCode,
                    d.FallbackSriPaymentMethodCode,
                    d.DefaultWarehouseSource,
                    d.RequiresManualWarehouseSelection,
                    d.ConfigurationWarnings
                )
            )
        );
    }
}

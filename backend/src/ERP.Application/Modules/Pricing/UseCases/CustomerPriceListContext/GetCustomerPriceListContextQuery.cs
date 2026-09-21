using ERP.Application.Common;
using ERP.Application.Modules.Pricing.Services;
using MediatR;

namespace ERP.Application.Modules.Pricing.UseCases.CustomerPriceListContext;

/// <summary>
/// SALES-PRICING-UX-TRACEABILITY-07C: contexto read-only de listas de precios de un cliente —
/// propiedad del módulo Pricing, consumido por la UI de Ventas para la cabecera ("Lista
/// preferente"). Reutiliza <see cref="IPriceListSelectionResolver"/> (única fuente de verdad de
/// qué listas son candidatas); no consulta PriceListCustomer directamente ni reimplementa el
/// orden de precedencia.
/// </summary>
public sealed record GetCustomerPriceListContextQuery(Guid CustomerId)
    : IRequest<Result<CustomerPriceListContextDto>>,
        ICompanyScopedRequest;

/// <summary>
/// <c>CustomerPriceList*</c> es SOLO el candidato <see cref="PriceListSelectionSource.Customer"/>
/// (lista explícitamente asignada al cliente) y <c>CompanyDefaultPriceList*</c> es SOLO el
/// candidato <see cref="PriceListSelectionSource.CompanyDefault"/> — la lista default de la
/// empresa nunca se presenta como lista del cliente. Si la lista del cliente y la default son la
/// misma PriceList, el resolver devuelve un único candidato Customer (ver
/// IPriceListSelectionResolver), así que CompanyDefault queda null en ese caso.
/// </summary>
public sealed record CustomerPriceListContextDto(
    Guid? CustomerPriceListId,
    string? CustomerPriceListName,
    Guid? CompanyDefaultPriceListId,
    string? CompanyDefaultPriceListName
);

public sealed class GetCustomerPriceListContextQueryHandler
    : IRequestHandler<GetCustomerPriceListContextQuery, Result<CustomerPriceListContextDto>>
{
    private readonly IPriceListSelectionResolver _selection;

    public GetCustomerPriceListContextQueryHandler(IPriceListSelectionResolver selection) =>
        _selection = selection;

    public async Task<Result<CustomerPriceListContextDto>> Handle(
        GetCustomerPriceListContextQuery request,
        CancellationToken ct
    )
    {
        var candidates = await _selection.ResolveAsync(request.CustomerId, ct);
        var customer = candidates.FirstOrDefault(c => c.Source == PriceListSelectionSource.Customer);
        var companyDefault = candidates.FirstOrDefault(c =>
            c.Source == PriceListSelectionSource.CompanyDefault
        );

        return Result<CustomerPriceListContextDto>.Success(
            new CustomerPriceListContextDto(
                customer?.PriceListId,
                customer?.PriceListName,
                companyDefault?.PriceListId,
                companyDefault?.PriceListName
            )
        );
    }
}

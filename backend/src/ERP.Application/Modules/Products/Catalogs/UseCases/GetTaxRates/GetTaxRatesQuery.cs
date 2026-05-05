using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.Catalogs.UseCases.GetTaxRates;

public sealed record GetTaxRatesQuery(
    TaxRateType? Type,
    bool OnlyActive
) : IRequest<Result<IReadOnlyList<TaxRateDto>>>;

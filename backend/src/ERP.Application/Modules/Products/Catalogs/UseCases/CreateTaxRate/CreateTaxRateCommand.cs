using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.Catalogs.UseCases.CreateTaxRate;

public record CreateTaxRateCommand(
    string Code,
    string Name,
    TaxRateType Type,
    decimal Percentage
) : IRequest<Result<TaxRateDto>>;


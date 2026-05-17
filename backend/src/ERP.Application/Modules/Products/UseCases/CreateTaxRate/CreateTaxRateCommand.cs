using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.UseCases.CreateTaxRate;

public record CreateTaxRateCommand(
    string Code,
    string Name,
    TaxRateType Type,
    decimal Percentage
) : IRequest<Result<TaxRateDto>>;


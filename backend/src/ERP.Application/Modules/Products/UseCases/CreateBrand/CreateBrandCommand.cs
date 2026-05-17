using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateBrand;

public record CreateBrandCommand(
    string Code,
    string Name,
    string? Manufacturer     = null,
    string? CountryOfOrigin  = null
) : IRequest<Result<BrandDto>>;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateBrand;

public record CreateBrandCommand(
    string Code,
    string Name,
    string? Manufacturer     = null,
    string? CountryOfOrigin  = null
) : IRequest<Result<BrandDto>>;

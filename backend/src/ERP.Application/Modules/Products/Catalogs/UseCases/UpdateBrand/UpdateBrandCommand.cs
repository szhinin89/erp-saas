using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateBrand;

public record UpdateBrandCommand(
    Guid   BrandId,
    string Code,
    string Name,
    string? Manufacturer    = null,
    string? CountryOfOrigin = null
) : IRequest<Result<BrandDto>>;

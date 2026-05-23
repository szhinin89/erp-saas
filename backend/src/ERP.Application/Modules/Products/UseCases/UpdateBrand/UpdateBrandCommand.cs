using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.UpdateBrand;

public record UpdateBrandCommand(
    Guid   BrandId,
    string Code,
    string Name,
    string? Manufacturer    = null,
    string? CountryOfOrigin = null
) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

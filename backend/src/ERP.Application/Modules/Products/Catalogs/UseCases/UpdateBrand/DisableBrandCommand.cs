using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateBrand;

public record DisableBrandCommand(Guid BrandId) : IRequest<Result<BrandDto>>;

public record EnableBrandCommand(Guid BrandId) : IRequest<Result<BrandDto>>;

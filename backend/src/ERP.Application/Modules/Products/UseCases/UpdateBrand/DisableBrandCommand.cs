using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.UpdateBrand;

public record DisableBrandCommand(Guid BrandId) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

public record EnableBrandCommand(Guid BrandId) : IRequest<Result<BrandDto>>, ICompanyScopedRequest;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetBrands;

public sealed record GetBrandsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<BrandDto>>>;

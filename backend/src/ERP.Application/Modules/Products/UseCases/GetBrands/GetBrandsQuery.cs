using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetBrands;

public sealed record GetBrandsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<BrandDto>>>, ICompanyScopedRequest;

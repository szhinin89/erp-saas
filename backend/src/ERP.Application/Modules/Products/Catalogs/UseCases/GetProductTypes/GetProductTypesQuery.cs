using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductTypes;

public sealed record GetProductTypesQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<ProductTypeDto>>>;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProductTypes;

public sealed record GetProductTypesQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<ProductTypeDto>>>;

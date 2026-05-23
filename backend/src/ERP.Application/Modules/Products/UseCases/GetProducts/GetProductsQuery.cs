using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProducts;

public sealed record GetProductsQuery : IRequest<Result<IReadOnlyList<ProductDto>>>, ICompanyScopedRequest;

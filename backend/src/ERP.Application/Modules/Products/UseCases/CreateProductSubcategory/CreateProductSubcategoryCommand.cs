using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateProductSubcategory;

public record CreateProductSubcategoryCommand(string Code, string Name, Guid CategoryId) : IRequest<Result<ProductSubcategoryDto>>, ICompanyScopedRequest;

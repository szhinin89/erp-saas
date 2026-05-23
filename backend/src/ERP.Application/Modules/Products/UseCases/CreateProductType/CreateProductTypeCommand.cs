using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateProductType;

public record CreateProductTypeCommand(string Code, string Name) : IRequest<Result<ProductTypeDto>>, ICompanyScopedRequest;


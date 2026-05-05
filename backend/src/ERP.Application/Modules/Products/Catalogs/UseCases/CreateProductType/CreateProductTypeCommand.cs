using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductType;

public record CreateProductTypeCommand(string Code, string Name) : IRequest<Result<ProductTypeDto>>;


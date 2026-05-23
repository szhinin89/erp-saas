using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateProductLine;

public record CreateProductLineCommand(string Code, string Name) : IRequest<Result<ProductLineDto>>, ICompanyScopedRequest;

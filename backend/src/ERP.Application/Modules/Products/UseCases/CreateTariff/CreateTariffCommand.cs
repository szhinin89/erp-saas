using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateTariff;

public record CreateTariffCommand(string Code, string Description) : IRequest<Result<TariffDto>>;


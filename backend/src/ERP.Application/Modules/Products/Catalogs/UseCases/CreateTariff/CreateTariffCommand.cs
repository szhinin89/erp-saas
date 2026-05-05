using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateTariff;

public record CreateTariffCommand(string Code, string Description) : IRequest<Result<TariffDto>>;


using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetTariffs;

public sealed record GetTariffsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<TariffDto>>>;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetTariffs;

public sealed record GetTariffsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<TariffDto>>>, ICompanyScopedRequest;

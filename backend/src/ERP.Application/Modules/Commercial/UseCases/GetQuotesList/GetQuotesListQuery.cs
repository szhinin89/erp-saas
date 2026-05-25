using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetQuotesList;

public sealed record GetQuotesListQuery(
    int PageNumber,
    int PageSize,
    Guid? BusinessPartnerId,
    string? Status,
    DateOnly? DateFrom,
    DateOnly? DateTo) : IRequest<Result<QuotesPagedResult>>, ICompanyScopedRequest;

using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetQuoteByPublicId;

public sealed record GetQuoteByPublicIdQuery(Guid PublicId)
    : IRequest<Result<QuoteDetailDto?>>, ICompanyScopedRequest;

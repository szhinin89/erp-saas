using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.CancelQuote;

public sealed record CancelQuoteCommand(Guid PublicId, string? Reason = null)
    : IRequest<Result<QuoteDto>>;

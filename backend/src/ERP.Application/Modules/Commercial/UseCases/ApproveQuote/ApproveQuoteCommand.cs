using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.ApproveQuote;

public sealed record ApproveQuoteCommand(Guid PublicId) : IRequest<Result<QuoteDto>>;

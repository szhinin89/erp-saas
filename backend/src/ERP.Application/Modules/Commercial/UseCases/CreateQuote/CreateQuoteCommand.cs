using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.CreateQuote;

public sealed record CreateQuoteCommand(
    Guid BusinessPartnerId,
    DateOnly ValidUntil,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<QuoteLineInputDto> Lines) : IRequest<Result<QuoteDto>>;

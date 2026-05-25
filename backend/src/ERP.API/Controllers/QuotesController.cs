using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Contracts.Sales;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Application.Modules.Commercial.UseCases.ApproveQuote;
using ERP.Application.Modules.Commercial.UseCases.CancelQuote;
using ERP.Application.Modules.Commercial.UseCases.CreateQuote;
using ERP.Application.Modules.Commercial.UseCases.GetQuoteByPublicId;
using ERP.Application.Modules.Commercial.UseCases.GetQuotesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ventas", Permissions.SalesQuote.View, "📋", "/sales/quotes", null, 45)]
[ApiController]
[Route("api/sales/quotes")]
[Authorize]
[Produces("application/json")]
public sealed class QuotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuotesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:sales.quotes.view")]
    [ProducesResponseType(typeof(ApiResponse<QuotesPagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var (pageNumber, pageSize) = ListQueryParameters.ParsePaged(Request.Query);
        var businessPartnerId = ListQueryParameters.ParseOptionalGuid(Request.Query, "businessPartnerId");
        var status = ListQueryParameters.ParseOptionalString(Request.Query, "status");
        var (dateFrom, dateTo) = ListQueryParameters.ParseDateOnlyRange(Request.Query);

        var result = await _mediator.Send(
            new GetQuotesListQuery(pageNumber, pageSize, businessPartnerId, status, dateFrom, dateTo),
            ct);

        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("{publicId:guid}")]
    [Authorize(Policy = "perm:sales.quotes.view")]
    [ProducesResponseType(typeof(ApiResponse<QuoteDetailDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPublicId(Guid publicId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetQuoteByPublicIdQuery(publicId), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost]
    [Authorize(Policy = "perm:sales.quotes.create")]
    [ProducesResponseType(typeof(ApiResponse<QuoteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateQuoteRequest request, CancellationToken ct)
    {
        var command = new CreateQuoteCommand(
            request.BusinessPartnerId,
            request.ValidUntil,
            request.PaymentTermDays,
            request.Notes,
            request.Lines.Select(l => new QuoteLineInputDto(
                l.ProductId, l.Quantity, l.UnitPrice, l.TaxRatePct)).ToList());

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPost("{publicId:guid}/approve")]
    [Authorize(Policy = "perm:sales.quotes.update")]
    [ProducesResponseType(typeof(ApiResponse<QuoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid publicId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApproveQuoteCommand(publicId), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("{publicId:guid}/cancel")]
    [Authorize(Policy = "perm:sales.quotes.update")]
    [ProducesResponseType(typeof(ApiResponse<QuoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid publicId, [FromBody] CancelQuoteRequest? request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelQuoteCommand(publicId, request?.Reason), ct);
        return this.ToOkOrBadRequest(result);
    }
}

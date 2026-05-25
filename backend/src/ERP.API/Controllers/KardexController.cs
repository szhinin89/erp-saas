using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common.Config;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.GetKardex;

namespace ERP.API.Controllers;

[AppFeature("Kardex", "perm:inventory.kardex.view", "📊", "/inventory/kardex", "perm:inventory.products.view", 42)]
[ApiController]
[Route("api/inventory/kardex")]
[Authorize]
[Produces("application/json")]
public sealed class KardexController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly KardexOptions _opts;

    public KardexController(IMediator mediator, IOptions<KardexOptions> opts)
    {
        _mediator = mediator;
        _opts = opts.Value;
    }

    [HttpGet]
    [Authorize(Policy = "perm:inventory.kardex.view")]
    [ProducesResponseType(typeof(ApiResponse<KardexResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> GetKardex(CancellationToken ct = default)
    {
        var (ok, productId, warehouseId, startDate, endDate, err) = KardexQueryParameters.Parse(Request.Query);
        if (!ok) return err!;

        if (KardexAsyncHelper.ShouldUseAsync(_opts, startDate, endDate))
            return await KardexAsyncHelper.EnqueueAsync(_mediator, Response, productId, warehouseId, startDate, endDate, ct);

        var result = await _mediator.Send(new GetKardexQuery(productId, warehouseId, startDate, endDate), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }
}

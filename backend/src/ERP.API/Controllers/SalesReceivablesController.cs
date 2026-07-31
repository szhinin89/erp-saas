using ERP.API.Extensions;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md, 2026-07-30): la pantalla de consulta de CxC
/// (/finance/receivables) consume este controller. Sin [AppFeature]: AppFeatureDiscoveryService
/// usa Permission como clave única de menú (Dictionary&lt;string, DiscoveryRow&gt;) — reutilizar
/// $"perm:{SalesPermissions.View}" (ya usado por SalesController para "Ventas") colisionaría y
/// silenciosamente ocultaría uno de los dos ítems de menú. Registrar un permiso granular nuevo
/// (p. ej. finance.receivables.view) queda fuera de este alcance — ver informe de cierre P0-03.
/// La pantalla es alcanzable hoy solo por URL directa. La liquidación (registrar cobro) vive en
/// FinancePaymentsController/RegisterCollectionCommand, no aquí.
/// </summary>
[ApiController]
[Route("api/v1/sales-receivables")]
[Authorize]
[Produces("application/json")]
public sealed class SalesReceivablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesReceivablesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-invoice/{invoiceId:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetByInvoice(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetReceivableByInvoiceQuery(invoiceId), ct));

    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetReceivablesListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );
}

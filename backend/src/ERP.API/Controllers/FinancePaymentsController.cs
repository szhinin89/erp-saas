using ERP.API.Extensions;
using ERP.Application.Modules.Finance.UseCases.Payments;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — expone la liquidación de AR (<c>Payment</c>,
/// Fase 5.5.5.3). Controller delgado: solo recibe el command, lo envía vía MediatR y traduce el
/// <c>Result</c> — toda la lógica de negocio (balance, límites de saldo, transiciones de estado)
/// vive en <c>Payment</c>/<c>SalesReceivable</c> (<see cref="RegisterCollectionCommandHandler"/>),
/// reutilizada sin cambios.
/// PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — el endpoint AP (<c>POST /payments</c>,
/// <c>RegisterPaymentCommand</c> contra <c>AccountsPayable</c>) fue eliminado junto con su única
/// UI (<c>AccountsPayablePage</c>/<c>RegisterPaymentModal</c>, ya eliminadas): no debe quedar un
/// endpoint activo de registro de pago a proveedor sin pantalla ni diseño — cuando exista el
/// módulo PagoCabecera/PagoDetalle, se implementará ahí desde cero, no reviviendo este endpoint.
/// </summary>
[ApiController]
[Route("api/v1/finance")]
[Authorize]
[Produces("application/json")]
public sealed class FinancePaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinancePaymentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Registra un cobro (AR) aplicado contra una o más CxC del cliente indicado.</summary>
    [HttpPost("collections")]
    [Authorize(Policy = $"perm:{FinancePermissions.Create}")]
    public async Task<IActionResult> RegisterCollection(
        [FromBody] RegisterCollectionCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));
}

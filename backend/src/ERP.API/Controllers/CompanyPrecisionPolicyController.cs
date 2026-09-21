using ERP.API.Extensions;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Nunca acepta tenantId/companyId desde body/query — siempre
/// del contexto autenticado (JWT vía ICurrentTenant/ICurrentCompany, resuelto dentro de los
/// handlers). No expone endpoint de unlock: una policy bloqueada (IsLocked=true) solo puede
/// desbloquearse por soporte fuera de este ticket.
/// </summary>
[ApiController]
[Route("api/v1/config/precision-policy")]
[Authorize]
[Produces("application/json")]
public sealed class CompanyPrecisionPolicyController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyPrecisionPolicyController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetCompanyPrecisionPolicyQuery(), ct), "OK");

    /// <summary>Definiciones (keys/rangos/defaults) y perfiles predefinidos — metadata estática.</summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPrecisionPolicyMetadataQuery(), ct), "OK");

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCompanyPrecisionPolicyCommand cmd,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
}

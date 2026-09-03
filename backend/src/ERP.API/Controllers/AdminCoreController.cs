using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.ListCompaniesForAdminCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// AdminGlobalCore: dashboard global de empresas por tenant. Solo accesible con token global
/// (tenant_id == Guid.Empty + rol Admin) — ver policy "PlatformAdmin" en Program.cs. Nunca
/// consumido por el ERP operativo (AppLayout/menú de tenant).
/// </summary>
[AppFeature(
    "Admin Core",
    "perm:admin_core.dashboard",
    "🌐",
    null,
    null,
    991,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/admin-core")]
[Authorize(Policy = "PlatformAdmin")]
[Produces("application/json")]
public sealed class AdminCoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCoreController(IMediator mediator) => _mediator = mediator;

    [HttpGet("companies")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<AdminCoreCompanyDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> ListCompanies(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCompaniesForAdminCoreQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<AdminCoreCompanyDto>());
    }
}

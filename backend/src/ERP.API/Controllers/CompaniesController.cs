using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.CreateCompany;
using ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Empresa activa",
    $"perm:{SettingsPermissions.CompaniesView}",
    "🏢",
    "/settings/company",
    null,
    28,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/companies")]
[Produces("application/json")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("current")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCurrentCompanyQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost]
    [Authorize(Policy = "CompanyProvisioning")]
    [ProducesResponseType(typeof(ApiResponse<CompanyCreatedDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        // La respuesta puede incluir las credenciales temporales del administrador inicial.
        Response.Headers.CacheControl = "no-store";
        return this.ToCreatedOrBadRequest(result);
    }
}

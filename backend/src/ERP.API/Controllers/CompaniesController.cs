using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.CreateCompany;
using ERP.Application.Modules.Companies.UseCases.GetCompanyById;
using ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;
using ERP.Application.Modules.Companies.UseCases.ListCompanies;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Empresas operativas",
    $"perm:{SettingsPermissions.CompaniesView}",
    "🏢",
    "/companies",
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

    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new ListCompaniesQuery(activeOnly), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CompanyListItemDto>());
    }

    [HttpGet("current")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCurrentCompanyQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyByIdQuery(id), cancellationToken);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "CompanyProvisioning")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCompanyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

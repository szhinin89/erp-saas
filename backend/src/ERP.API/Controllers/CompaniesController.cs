using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Platform.Companies;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Application.Modules.Platform.Companies.UseCases.CreateCompany;
using ERP.Application.Modules.Platform.Companies.UseCases.GetCompanyById;
using ERP.Application.Modules.Platform.Companies.UseCases.GetCurrentCompany;
using ERP.Application.Modules.Platform.Companies.UseCases.ListCompanies;
using ERP.Application.Modules.Platform.Companies.UseCases.UpdateCompany;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Empresas operativas",
    CompanyPermissions.PolicyView,
    "🏢",
    "/saas/companies",
    null,
    28,
    IsVisibleInMenu = false)]
[ApiController]
[Route("api/companies")]
[Authorize]
[Produces("application/json")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = CompanyPermissions.PolicyView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListCompaniesQuery(activeOnly), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CompanyListItemDto>());
    }

    [HttpGet("current")]
    [Authorize(Policy = CompanyPermissions.PolicyView)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCurrentCompanyQuery(), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = CompanyPermissions.PolicyView)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCompanyByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = CompanyPermissions.PolicyCreate)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Empresa creada");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = CompanyPermissions.PolicyUpdate)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Empresa actualizada");
    }
}

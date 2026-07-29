using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/org-config")]
[Authorize]
[Produces("application/json")]
public sealed class OrgConfigController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrgConfigController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve las configuraciones de factura de venta a nivel empresa.</summary>
    [HttpGet("company/invoice-defaults")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompanyView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyInvoiceOrgSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyInvoiceDefaults(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyInvoiceOrgSettingsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza las configuraciones de factura de venta a nivel empresa.</summary>
    [HttpPut("company/invoice-defaults")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyInvoiceOrgSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertCompanyInvoiceDefaults(
        [FromBody] UpsertCompanyInvoiceOrgSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Devuelve las configuraciones de factura de venta a nivel sucursal.</summary>
    [HttpGet("branch/{branchId:guid}/invoice-defaults")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompanyView}")]
    [ProducesResponseType(typeof(ApiResponse<BranchInvoiceOrgSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranchInvoiceDefaults(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBranchInvoiceOrgSettingsQuery(branchId), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza las configuraciones de factura de venta a nivel sucursal.</summary>
    [HttpPut("branch/{branchId:guid}/invoice-defaults")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchInvoiceOrgSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertBranchInvoiceDefaults(
        Guid branchId,
        [FromBody] UpsertBranchInvoiceOrgSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command with { BranchId = branchId }, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

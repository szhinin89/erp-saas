using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.CreateCompany;
using ERP.Application.Modules.Companies.UseCases.GetCompanyById;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoAltContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;
using ERP.Application.Modules.Companies.UseCases.ListCompanies;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyOperation;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogo;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogoAlt;
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
    IsVisibleInMenu = false)]
[ApiController]
[Route("api/v1/companies")]
[Authorize]
[Produces("application/json")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
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

    [HttpGet("profile")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyProfileQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCompanyProfileCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("profile/logo")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogo(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        var content = new MediaUploadContent(stream, file.FileName, file.ContentType, file.Length);
        var result = await _mediator.Send(new UploadCompanyLogoCommand(content), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profile/logo/content")]
    public async Task<IActionResult> GetLogoContent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyLogoContentQuery(), cancellationToken);
        if (!result.IsSuccess)
            return this.ApiNotFound(result.Error ?? "Logo no encontrado.");

        var content = result.Value!;
        return File(content.Content, content.ContentType, content.FileName);
    }

    [HttpPut("profile/fiscal")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFiscal([FromBody] UpdateCompanyFiscalCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/operation")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOperation([FromBody] UpdateCompanyOperationCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/documents")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDocuments([FromBody] UpdateCompanyDocumentsCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/branding")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBranding([FromBody] UpdateCompanyBrandingCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("profile/logo-alt")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogoAlt(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        var content = new MediaUploadContent(stream, file.FileName, file.ContentType, file.Length);
        var result = await _mediator.Send(new UploadCompanyLogoAltCommand(content), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profile/logo-alt/content")]
    public async Task<IActionResult> GetLogoAltContent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyLogoAltContentQuery(), cancellationToken);
        if (!result.IsSuccess)
            return this.ApiNotFound(result.Error ?? "Logo alternativo no encontrado.");

        var content = result.Value!;
        return File(content.Content, content.ContentType, content.FileName);
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
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyCommand command, CancellationToken cancellationToken = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

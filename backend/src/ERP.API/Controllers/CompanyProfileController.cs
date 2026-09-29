using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Uploads;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.GetCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoAltContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;
using ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.GetSalesFiscalPolicy;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyOperation;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogo;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogoAlt;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// ZH-API-THIN-COMPANIES-01 — perfil y configuración de la EMPRESA ACTIVA (perfil, fiscal, operación,
// documentos, branding, logos, política fiscal de ventas, preparación operativa). Consumido por
// configuracion/empresa en el frontend. La administración de empresas del tenant (listar, crear,
// editar por id, empresa actual) sigue en CompaniesController.
// Contrato intacto: mismas rutas bajo api/v1/companies, mismas policies, mismo tag OpenAPI
// "Companies" y sin [AppFeature] propio (la fila "Empresas operativas" la aporta CompaniesController).
// Comentario no-XML a propósito: un <summary> de clase agregaría un tag nuevo al documento OpenAPI.
[ApiController]
[Route("api/v1/companies")]
[Tags("Companies")]
[Produces("application/json")]
public sealed class CompanyProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyProfileController(IMediator mediator) => _mediator = mediator;

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
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCompanyProfileCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("profile/logo")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogo(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var upload = await BufferedFormFile.CreateAsync(file, cancellationToken);
        var result = await _mediator.Send(new UploadCompanyLogoCommand(upload.Content), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profile/logo/content")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    public async Task<IActionResult> GetLogoContent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyLogoContentQuery(), cancellationToken);
        return this.ToFileOrNotFound(result, content => File(content.Content, content.ContentType, content.FileName));
    }

    [HttpPut("profile/fiscal")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFiscal(
        [FromBody] UpdateCompanyFiscalCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/operation")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOperation(
        [FromBody] UpdateCompanyOperationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/documents")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDocuments(
        [FromBody] UpdateCompanyDocumentsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profile/branding")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyBrandingQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/branding")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBranding(
        [FromBody] UpdateCompanyBrandingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("profile/logo-alt")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogoAlt(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var upload = await BufferedFormFile.CreateAsync(file, cancellationToken);
        var result = await _mediator.Send(
            new UploadCompanyLogoAltCommand(upload.Content),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    [HttpGet("profile/logo-alt/content")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    public async Task<IActionResult> GetLogoAltContent(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetCompanyLogoAltContentQuery(), cancellationToken);
        return this.ToFileOrNotFound(result, content => File(content.Content, content.ContentType, content.FileName));
    }

    [HttpGet("profile/fiscal-policy")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<ERP.Application.Modules.Sales.DTOs.SalesFiscalPolicyDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetFiscalPolicy(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSalesFiscalPolicyQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// COMPANY-OPERATING-SETUP-01: checklist de preparación operativa — orquesta fuentes/resolvers
    /// existentes, no persiste nada. Ver ICompanyOperationalReadinessResolver.
    /// </summary>
    [HttpGet("operational-readiness")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyOperationalReadinessDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetOperationalReadiness(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyOperationalReadinessQuery(),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    [HttpPut("profile/fiscal-policy")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(
        typeof(ApiResponse<ERP.Application.Modules.Sales.DTOs.SalesFiscalPolicyDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> UpdateFiscalPolicy(
        [FromBody] UpdateConsumerFinalMaxAmountCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

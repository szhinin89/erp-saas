using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.ActivateBusinessPartner;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.DisableBusinessPartner;
using ERP.Application.MasterData.UseCases.GetBusinessPartner;
using ERP.Application.MasterData.UseCases.GetCompanyBpSettings;
using ERP.Application.MasterData.UseCases.SearchBusinessPartners;
using ERP.Application.MasterData.UseCases.UpdateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpdateCustomerNotes;
using ERP.Application.MasterData.UseCases.UpdateSupplierProfile;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// MasterData BC — BusinessPartner CRUD.
///
/// Scope: subscriber (no company). Un mismo RUC/CI existe una sola vez por Subscriber.
/// Todos los endpoints filtran por subscriber_id del JWT via EF query filter global.
/// RBAC: permisos granulares masterdata.businesspartners.* — no Session genérica.
///
/// Ver docs/arch/BUSINESSPARTNER-ADR.md para decisiones de dominio.
/// </summary>
[ApiController]
[Route("api/master/business-partners")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData")]
public sealed class BusinessPartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessPartnersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Busca BusinessPartners del subscriber activo.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BusinessPartnerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q          = null,
        [FromQuery] bool?   isActive   = true,
        [FromQuery] bool?   isCustomer = null,
        [FromQuery] bool?   isSupplier = null,
        [FromQuery] int     skip       = 0,
        [FromQuery] int     take       = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchBusinessPartnersQuery(q, isActive, isCustomer, isSupplier, skip, take), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Obtiene un BusinessPartner por Id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBusinessPartnerQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiNotFound(result.Error ?? "BusinessPartner no encontrado.");
        return this.ApiOk(result.Value!);
    }

    /// <summary>
    /// Crea un BusinessPartner en el subscriber activo.
    /// Puede asignarse como cliente, proveedor o ambos simultáneamente.
    /// Retorna error 400 si el número de identificación ya existe para este subscriber.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Create}")]
    [ProducesResponseType(typeof(ApiResponse<BusinessPartnerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessPartnerRequest body,
        CancellationToken ct = default)
    {
        var command = new CreateBusinessPartnerCommand(
            body.IdentificationType,
            body.IdentificationNumber,
            body.LegalName,
            body.TradeName,
            body.Email,
            body.Phone,
            body.CountryCode,
            body.AsCustomer,
            body.AsSupplier);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza los datos de perfil e identificación de un BusinessPartner.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateBusinessPartnerRequest body,
        CancellationToken ct = default)
    {
        var command = new UpdateBusinessPartnerCommand(
            id,
            body.IdentificationType,
            body.IdentificationNumber,
            body.LegalName,
            body.TradeName,
            body.Email,
            body.Phone,
            body.CountryCode);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Desactiva un BusinessPartner. Soft delete — no elimina el registro.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Disable}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableBusinessPartnerCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva un BusinessPartner previamente desactivado.</summary>
    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ActivateBusinessPartnerCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene las condiciones comerciales de un BP para la Company activa.
    /// Retorna null si no se han configurado aún.
    /// </summary>
    [HttpGet("{id:guid}/company-settings")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.View}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBpSettingsDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanySettings([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCompanyBpSettingsQuery(id), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza las condiciones comerciales de un BusinessPartner para la Company activa.
    /// Requiere company_id en el JWT. Los campos (CreditLimit, PaymentDays, IsBlocked) son por empresa.
    /// </summary>
    [HttpPatch("{id:guid}/company-settings")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.ConfigureCompany}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertCompanySettings(
        [FromRoute] Guid id,
        [FromBody] UpsertCompanyBpSettingsRequest body,
        CancellationToken ct = default)
    {
        var command = new UpsertCompanyBpSettingsCommand(
            id,
            body.CreditLimit,
            body.PaymentDays,
            body.IsBlocked);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza las notas del perfil de cliente.</summary>
    [HttpPatch("{id:guid}/customer-notes")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomerNotes(
        [FromRoute] Guid id,
        [FromBody] UpdateCustomerNotesRequest body,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateCustomerNotesCommand(id, body.Notes), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Actualiza los campos SRI del perfil de proveedor.</summary>
    [HttpPatch("{id:guid}/supplier-profile")]
    [Authorize(Policy = $"perm:{Permissions.MasterDataBusinessPartner.Update}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplierProfile(
        [FromRoute] Guid id,
        [FromBody] UpdateSupplierProfileRequest body,
        CancellationToken ct = default)
    {
        var command = new UpdateSupplierProfileCommand(
            id,
            body.DefaultTaxSupportCode,
            body.DefaultRetentionVatCode,
            body.DefaultRetentionIncomeCode,
            body.PaymentTerms);

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }
}

public sealed class UpsertCompanyBpSettingsRequest
{
    public decimal? CreditLimit  { get; set; }
    public short    PaymentDays  { get; set; }
    public bool     IsBlocked    { get; set; }
}

public sealed class CreateBusinessPartnerRequest
{
    public string  IdentificationType   { get; set; } = "";
    public string  IdentificationNumber { get; set; } = "";
    public string  LegalName            { get; set; } = "";
    public string? TradeName            { get; set; }
    public string? Email                { get; set; }
    public string? Phone                { get; set; }
    public string? CountryCode          { get; set; }
    public bool    AsCustomer           { get; set; }
    public bool    AsSupplier           { get; set; }
}

public sealed class UpdateBusinessPartnerRequest
{
    public string  IdentificationType   { get; set; } = "";
    public string  IdentificationNumber { get; set; } = "";
    public string  LegalName            { get; set; } = "";
    public string? TradeName            { get; set; }
    public string? Email                { get; set; }
    public string? Phone                { get; set; }
    public string? CountryCode          { get; set; }
}

public sealed class UpdateCustomerNotesRequest
{
    public string? Notes { get; set; }
}

public sealed class UpdateSupplierProfileRequest
{
    public string? DefaultTaxSupportCode      { get; set; }
    public string? DefaultRetentionVatCode    { get; set; }
    public string? DefaultRetentionIncomeCode { get; set; }
    public string? PaymentTerms               { get; set; }
}

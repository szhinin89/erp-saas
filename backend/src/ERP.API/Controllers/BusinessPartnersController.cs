using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.DisableBusinessPartner;
using ERP.Application.MasterData.UseCases.GetBusinessPartner;
using ERP.Application.MasterData.UseCases.SearchBusinessPartners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// MasterData BC — BusinessPartner CRUD.
///
/// Scope: subscriber (no company). Un mismo RUC/CI existe una sola vez por Subscriber.
/// Todos los endpoints filtran por subscriber_id del JWT via EF query filter global.
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
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BusinessPartnerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q        = null,
        [FromQuery] bool?   isActive = true,
        [FromQuery] int     skip     = 0,
        [FromQuery] int     take     = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchBusinessPartnersQuery(q, isActive, skip, take), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Obtiene un BusinessPartner por Id.</summary>
    [HttpGet("{id:guid}")]
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

    /// <summary>Desactiva un BusinessPartner. Soft delete — no elimina el registro.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable([FromRoute] Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableBusinessPartnerCommand(id), ct);
        return this.ToOkOrBadRequest(result);
    }
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

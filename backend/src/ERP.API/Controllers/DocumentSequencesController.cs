using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;
using ERP.Application.Modules.Company.UseCases.GetDocumentSequences;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-03 — administración mínima de <c>DocumentSequence</c> (secuencias
/// documentales SRI centralizadas, ADR-019).
///
/// DOCUMENT-SEQUENCES-CONFIG-UI-04 — agrega la pantalla de Settings ("Numeración de documentos")
/// para configurar el número inicial por establecimiento/punto de emisión/tipo de documento SRI;
/// <c>IsVisibleInMenu</c> pasa a <c>true</c> para que el ítem aparezca en el menú server-driven de
/// Settings (antes registraba el permiso en el catálogo sin ítem de navegación real, ver
/// docs/decisions/DOCUMENT-SEQUENCES-DESIGN-02.md). Se agrega un único endpoint de lectura
/// (<see cref="GetAll"/>) — gap detectado al revisar esta fase: no existía ninguna forma de listar
/// el estado de las secuencias ya configuradas/usadas, solo el PUT de configuración. No cambia
/// ninguna regla de dominio.
/// </summary>
[AppFeature(
    "Numeración de documentos",
    $"perm:{SettingsPermissions.DocumentSequencesManage}",
    "format_list_numbered",
    "/settings/document-sequences",
    "perm:settings.group",
    41
)]
[ApiController]
[Route("api/v1/settings/document-sequences")]
[Authorize]
[Produces("application/json")]
public sealed class DocumentSequencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentSequencesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// DOCUMENT-SEQUENCES-CONFIG-UI-04 — lista todas las secuencias documentales SRI ya
    /// configuradas/usadas de la empresa activa. Solo lectura — nunca captura numeración (eso
    /// sigue siendo exclusivo de <c>IDocumentSequenceRepository.CaptureNextAsync</c>, invocado
    /// desde los flujos de emisión reales, nunca desde aquí).
    /// </summary>
    /// <response code="200">Secuencias con su próximo secuencial (<c>nextNumber</c>) y si ya entregaron algún número.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<DocumentSequenceDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDocumentSequencesQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Configura el próximo secuencial de una secuencia documental SRI, antes de su primer uso
    /// real. Rechaza el ajuste (409) si la secuencia ya entregó al menos un número real — ver
    /// <c>DocumentSequence.ConfigureNextNumber</c>.
    /// </summary>
    /// <remarks>
    /// Si la secuencia del punto de emisión y tipo de comprobante aún no existe, la crea. El
    /// tipo de comprobante debe estar activo y habilitado para emisión electrónica en el catálogo SRI y
    /// el siguiente secuencial debe estar entre 1 y el máximo de 9 dígitos. No captura numeración ni
    /// crea documentos.
    /// </remarks>
    /// <param name="command">Punto de emisión, código de tipo de comprobante y siguiente secuencial.</param>
    /// <response code="200">Secuencia configurada; <c>nextNumber</c> es el próximo secuencial a entregar.</response>
    /// <response code="404">El punto de emisión no existe en la empresa activa.</response>
    /// <response code="409">La secuencia ya entregó al menos un número real y no se puede reconfigurar.</response>
    /// <response code="422">Tipo de comprobante no activo o no habilitado, o secuencial fuera de rango.</response>
    [HttpPut("configure")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentSequencesManage}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentSequenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Configure(
        [FromBody] ConfigureDocumentSequenceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}

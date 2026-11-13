using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Application.Modules.Ride.UseCases.RegenerateRide;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Expone Ride (ADR-025) vía HTTP — controller delgado: recibe el request, delega íntegramente a
/// MediatR, transforma <c>Result&lt;RideGenerationResultDto&gt;</c> a HTTP con el mismo patrón ya
/// usado por el resto del ERP (<see cref="ApiResultExtensions.ToOkOrBadRequest{T}"/>). Ninguna
/// lógica de negocio vive aquí — Company/Tenant Scope se resuelven automáticamente vía
/// <c>CompanyScopeBehavior</c> porque <c>GetOrGenerateRideQuery</c>/<c>RegenerateRideCommand</c>
/// (Fase 3, congelados) ya implementan <c>ICompanyScopedRequest</c>.
///
/// <see cref="GetContent"/> (Fase 12) es la única adición de superficie: ningún contrato público
/// de Ride devuelve los bytes del PDF (<c>RideGenerationResultDto.StoragePath</c> es solo la ruta
/// de almacenamiento), así que esta acción compone <c>GetOrGenerateRideQuery</c> (sin modificarlo)
/// con <see cref="IFileStorage"/> (interfaz ya existente, sin modificarla) para servir el binario
/// — mismo patrón que <c>CompaniesController.GetLogoContent</c>. No introduce lógica de negocio
/// nueva ni toca <c>RidePipeline</c>.
/// </summary>
[ApiController]
[Route("api/v1/ride")]
[Authorize]
[Produces("application/json")]
public sealed class RideController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _fileStorage;

    public RideController(IMediator mediator, IFileStorage fileStorage)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Obtiene el RIDE de un documento o lo genera si corresponde.
    /// </summary>
    /// <remarks>
    /// La respuesta siempre es 200 con el <c>outcome</c> correspondiente (incluidos <c>PendingSource</c> y
    /// <c>NotApplicable</c>). Un documento inexistente o de otra empresa no se distingue de «no aplica» (criterio
    /// de no enumeración).
    /// </remarks>
    /// <param name="sourceModule">Módulo de origen del documento (por ejemplo <c>Sales</c>).</param>
    /// <param name="sourceEntityId">Id del documento de origen en su módulo.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la generación del RIDE.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{RidePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<RideGenerationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrGenerate(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetOrGenerateRideQuery(sourceModule, sourceEntityId), ct),
            "OK"
        );

    /// <summary>
    /// Fuerza la regeneración del RIDE.
    /// </summary>
    /// <remarks>
    /// Regenera aunque la caché siga siendo válida.
    /// </remarks>
    /// <param name="command">Módulo y documento de origen.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la regeneración del RIDE.</response>
    [HttpPost("regenerate")]
    [Authorize(Policy = $"perm:{RidePermissions.Regenerate}")]
    [ProducesResponseType(typeof(ApiResponse<RideGenerationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Regenerate(
        [FromBody] RegenerateRideCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct), "OK");

    /// <summary>
    /// Descarga el PDF del RIDE de un documento.
    /// </summary>
    /// <remarks>
    /// Sirve los bytes del PDF ya generado (o lo genera si corresponde) con el mismo caso de uso que <c>GET
    /// ride</c>. El éxito es un archivo PDF (<c>application/pdf</c>, nombre <c>RIDE-{sourceEntityId}.pdf</c>), no
    /// un envelope JSON; los errores sí usan el envelope. Responde 404 si el documento todavía no tiene un RIDE
    /// disponible (<c>PendingSource</c>, <c>NotApplicable</c> o <c>Failed</c>) o si el archivo ya no está en el
    /// almacenamiento: el cliente debe consultar antes el <c>outcome</c>. El contrato publica el tipo de medio como
    /// <c>application/json</c> porque el <c>[Produces]</c> de clase prevalece.
    /// </remarks>
    /// <param name="sourceModule">Módulo de origen del documento (por ejemplo <c>Sales</c>).</param>
    /// <param name="sourceEntityId">Id del documento de origen en su módulo.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Archivo PDF del RIDE.</response>
    /// <response code="404">El documento no tiene un RIDE disponible todavía, o el archivo ya no está disponible en el almacenamiento.</response>
    [HttpGet("content")]
    [Authorize(Policy = $"perm:{RidePermissions.View}")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        CancellationToken ct
    )
    {
        var result = await _mediator.Send(
            new GetOrGenerateRideQuery(sourceModule, sourceEntityId),
            ct
        );
        if (!result.IsSuccess)
            return this.ApiFailure(result);

        var generation = result.Value!;
        if (
            generation.Outcome is not (RideOutcome.Generated or RideOutcome.Cached)
            || generation.StoragePath is null
        )
            return this.ApiNotFound("No hay un RIDE disponible para este documento todavía.");

        var stream = await _fileStorage.GetAsync(generation.StoragePath, ct);
        if (stream is null)
            return this.ApiNotFound("El archivo del RIDE no está disponible.");

        return File(stream, "application/pdf", $"RIDE-{sourceEntityId:N}.pdf");
    }
}

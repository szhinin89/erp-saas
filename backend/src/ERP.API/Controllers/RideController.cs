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
    /// Obtiene el RIDE ya generado o lo genera si corresponde. La respuesta siempre es 200 con el
    /// <c>Outcome</c> correspondiente (incluidos <c>PendingSource</c>/<c>NotApplicable</c>) — un
    /// documento inexistente o de otra empresa nunca es distinguible de "no aplica" en la
    /// respuesta (mismo criterio de no-enumeración que <c>ElectronicDocuments</c>).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{RidePermissions.View}")]
    public async Task<IActionResult> GetOrGenerate(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetOrGenerateRideQuery(sourceModule, sourceEntityId), ct),
            "OK"
        );

    /// <summary>Fuerza la regeneración del RIDE aunque el cache siga siendo válido.</summary>
    [HttpPost("regenerate")]
    [Authorize(Policy = $"perm:{RidePermissions.Regenerate}")]
    public async Task<IActionResult> Regenerate(
        [FromBody] RegenerateRideCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct), "OK");

    /// <summary>
    /// Sirve los bytes del PDF ya generado (o lo genera si corresponde), vía el mismo
    /// <c>GetOrGenerateRideQuery</c> que <see cref="GetOrGenerate"/>. 404 si el documento de
    /// origen todavía no tiene un RIDE disponible (<c>PendingSource</c>/<c>NotApplicable</c>/
    /// <c>Failed</c>) — el frontend siempre debe consultar el <c>Outcome</c> primero para mostrar
    /// el mensaje correcto y solo llamar aquí cuando corresponda.
    /// </summary>
    [HttpGet("content")]
    [Authorize(Policy = $"perm:{RidePermissions.View}")]
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
            return this.ApiBadRequest(result.Error ?? "No se pudo obtener el RIDE.");

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

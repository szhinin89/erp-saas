using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.DocTypes.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// DOCUMENT-FLOW-POLICY-01 — Configuración → Documentos → Políticas de documentos. Administra CÓMO se comporta
/// cada tipo de documento por empresa (<c>DocumentFlowPolicy</c>). Los permisos de esta pantalla
/// solo controlan el acceso a la configuración — nunca reemplazan los permisos de acción de cada
/// módulo (p. ej. <c>expenses.documents.cancel</c>), que se administran en Roles y Permisos.
/// </summary>
[AppFeature(
    "Políticas de documentos",
    $"perm:{SettingsPermissions.DocumentFlowsView}",
    "GitBranch",
    "/settings/document-flows",
    null,
    60
)]
[ApiController]
[Route("api/v1/settings/document-flows")]
[Authorize]
[Produces("application/json")]
public sealed class DocumentFlowPoliciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentFlowPoliciesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista las políticas de flujo documental de la empresa activa, con el nombre de su tipo de documento.</summary>
    /// <response code="200">Políticas de la empresa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<DocumentFlowPolicyDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetDocumentFlowPoliciesQuery(), ct), "OK");

    /// <summary>Obtiene una política de flujo documental de la empresa activa.</summary>
    /// <param name="id">Id de la política.</param>
    /// <response code="200">Política encontrada.</response>
    /// <response code="404">La política no existe en la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsView}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentFlowPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetDocumentFlowPolicyByIdQuery(id), ct));

    /// <summary>Actualiza la política de flujo documental de un tipo de documento.</summary>
    /// <remarks>
    /// Reemplaza todos los campos configurables de la política (estado activo y los modos de
    /// creación, confirmación, autorización, pendientes, anulación, cuentas por pagar, inventario y
    /// notificaciones); no hay actualización parcial. Controla cómo se comporta el documento, no quién
    /// puede ejecutar cada acción.
    /// </remarks>
    /// <param name="id">Id de la política; debe coincidir con <c>id</c> del cuerpo.</param>
    /// <param name="cmd">Valores completos de la política.</param>
    /// <response code="200">Política actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La política no existe en la empresa activa.</response>
    /// <response code="422">Id vacío o valores de modo no válidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentFlowPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDocumentFlowPolicyCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }
}

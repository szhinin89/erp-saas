using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.DocTypes.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// DOCUMENT-FLOW-POLICY-01 — Configuración → Documentos y flujos. Administra CÓMO se comporta
/// cada tipo de documento por empresa (<c>DocumentFlowPolicy</c>). Los permisos de esta pantalla
/// solo controlan el acceso a la configuración — nunca reemplazan los permisos de acción de cada
/// módulo (p. ej. <c>expenses.documents.cancel</c>), que se administran en Roles y Permisos.
/// </summary>
[AppFeature(
    "Documentos y flujos",
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

    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsView}")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetDocumentFlowPoliciesQuery(), ct), "OK");

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsView}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetDocumentFlowPolicyByIdQuery(id), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.DocumentFlowsUpdate}")]
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

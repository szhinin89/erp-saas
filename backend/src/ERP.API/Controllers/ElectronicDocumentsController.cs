using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicDocuments.UseCases.CreateElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDiagnosticBySource;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsDashboard;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsList;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentTimeline;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;
using ERP.Application.Modules.ElectronicDocuments.UseCases.RetryElectronicDocument;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Monitor de Documentos Electrónicos — consola de consulta del ciclo electrónico. No emite,
/// no firma ni cancela documentos. Acciones de escritura: reintentar manualmente un documento
/// varado (Signed/Received/DeadLetter/Failed), o registrar el documento faltante de un origen ya
/// autorizado comercialmente que nunca llegó a generar uno (backfill) — el resto de transiciones
/// las ejecuta <c>ElectronicDocumentIssuer</c>, invocado desde Ventas o desde el job automático
/// de reintentos.
/// </summary>
[AppFeature(
    "Monitor de Documentos Electrónicos",
    $"perm:{ElectronicDocumentsPermissions.View}",
    "📡",
    "/electronic-documents/monitor",
    "perm:settings.group",
    26
)]
[ApiController]
[Route("api/v1/electronic-documents")]
[Authorize]
[Produces("application/json")]
public sealed class ElectronicDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ElectronicDocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.View}")]
    public async Task<IActionResult> GetList(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? state = null,
        [FromQuery] string? documentType = null,
        [FromQuery] string? environment = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetElectronicDocumentsListQuery(
                    dateFrom,
                    dateTo,
                    state,
                    documentType,
                    environment,
                    search,
                    pageNumber,
                    pageSize
                ),
                ct
            ),
            "OK"
        );

    [HttpGet("dashboard")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.View}")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetElectronicDocumentsDashboardQuery(), ct),
            "OK"
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetElectronicDocumentDetailQuery(id), ct));

    /// <summary>
    /// Diagnóstico agnóstico de módulo — usado por cualquier pantalla del ERP (Ventas, y a futuro
    /// Retenciones/Notas/Guías) que conoce su propio <paramref name="sourceModule"/>/
    /// <paramref name="sourceEntityId"/> pero no el Id interno de <c>ElectronicDocument</c>.
    /// </summary>
    [HttpGet("by-source")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    public async Task<IActionResult> GetDiagnosticBySource(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(
                new GetElectronicDocumentDiagnosticBySourceQuery(sourceModule, sourceEntityId),
                ct
            )
        );

    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    public async Task<IActionResult> GetTimeline(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetElectronicDocumentTimelineQuery(id), ct));

    /// <summary>Devuelve el XML ya almacenado (borrador o firmado) — nunca genera uno nuevo.</summary>
    [HttpGet("xml")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    public async Task<IActionResult> GetXml(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        [FromQuery] ElectronicDocumentXmlVariant variant,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(
                new GetElectronicDocumentXmlQuery(sourceModule, sourceEntityId, variant),
                ct
            )
        );

    /// <summary>Reintenta manualmente un documento varado en Signed/Received/Failed, o lo reactiva desde DeadLetter y reintenta.</summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new RetryElectronicDocumentCommand(id), ct),
            "OK"
        );

    /// <summary>
    /// Backfill: registra el documento electrónico de un documento de origen ya autorizado
    /// comercialmente que nunca generó uno (p.ej. autorizado antes de que existiera esta
    /// infraestructura). Idempotente — Conflict si ya existe uno más allá de Draft/Failed.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
    public async Task<IActionResult> Register(
        [FromBody] CreateElectronicDocumentCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct), "OK");
}

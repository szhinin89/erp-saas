using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Uploads;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.FindItemMatches;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.GetLineMatch;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreateExpenseDraftFromReception;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.RefreshPurchaseReceptionStatus;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Purchases;

[AppFeature(
    "Recepción electrónica",
    $"perm:{PurchasePermissions.View}",
    "move_to_inbox",
    "/purchases/reception",
    $"perm:{PurchasePermissions.View}",
    61
)]
[ApiController]
[Route("api/v1/purchases/reception")]
[Authorize]
[Produces("application/json")]
// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3): consultar exige purchases.view; importar,
// consultar/guardar XML y preparar la compra exigen purchases.create; preparar un gasto,
// expenses.documents.create (403 antes del handler). Conciliar líneas (match/unmatch/correct/bulk/
// resolve) declara purchases.view como acceso al módulo y el handler exige el permiso exacto según el
// dato (IReceptionMatchingAccess): purchases.update si ya existe una compra activa de ese comprobante,
// purchases.create si no.
public sealed class PurchaseReceptionController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseReceptionController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Importa el TXT de comprobantes recibidos del SRI.
    /// </summary>
    /// <remarks>
    /// Solicitud <c>multipart/form-data</c> con el archivo en el campo <c>file</c>. Crea un documento de recepción
    /// por cada comprobante, en la sucursal activa; reimportar reutiliza los documentos propios. Antes de escribir,
    /// la identificación del receptor de cada fila debe pertenecer a la empresa operativa: una sola fila ajena o
    /// inválida rechaza el archivo completo con 422, indicando solo los números de línea. Si la clave de acceso ya
    /// pertenece a otra empresa del tenant, o la inserta una importación simultánea, el archivo completo se
    /// revierte con un 409 genérico. Los límites de tipo y tamaño los validan los validadores de Application.
    /// </remarks>
    /// <param name="file">Archivo TXT de comprobantes recibidos del SRI.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la importación.</response>
    /// <response code="400">No se adjuntó archivo o el archivo está vacío.</response>
    /// <response code="409">La clave de acceso ya pertenece a otra empresa del tenant o la insertó una importación simultánea; el archivo completo se revierte.</response>
    /// <response code="422">El archivo no supera la validación: receptor ajeno o inválido, o formato inválido.</response>
    [HttpPost("import")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionImportResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo.");

        await using var upload = await BufferedFormFile.CreateAsync(file, ct);
        var result = await _mediator.Send(new ImportPurchaseReceptionCommand(upload.Content), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Descarga el XML autorizado de un documento de recepción desde el SRI.
    /// </summary>
    /// <remarks>
    /// Solo opera sobre documentos en estado <c>Imported</c>; si falla, el documento conserva su estado. Al
    /// descargarlo se persisten sus líneas con la conciliación automática.
    /// </remarks>
    /// <param name="id">Id de el documento de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la descarga del XML.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/download-xml")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<DownloadPurchaseReceptionXmlResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadXml(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DownloadPurchaseReceptionXmlCommand(id), ct)
        );

    /// <summary>
    /// Actualiza el estado de documentos de recepción ya importados.
    /// </summary>
    /// <remarks>
    /// Recalcula el estado real de la compra, del documento, del proveedor, del gasto y de la nota de crédito sin
    /// reimportar el TXT ni descargar XML. Solo lectura; los ids que no pertenecen a la empresa activa se omiten.
    /// No consulta al SRI.
    /// </remarks>
    /// <param name="body">Ids de los documentos de recepción a actualizar.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado actualizado de los documentos.</response>
    [HttpPost("documents/refresh-status")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseReceptionItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> RefreshStatus(
        [FromBody] RefreshPurchaseReceptionStatusRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new RefreshPurchaseReceptionStatusQuery(body.DocumentIds), ct)
        );

    /// <summary>
    /// Descarga en lote los XML autorizados pendientes.
    /// </summary>
    /// <remarks>
    /// Descarga desde el SRI el XML autorizado de los documentos indicados que aún no lo tienen, reutilizando por
    /// documento el mismo caso de uso de la descarga individual. No crea compras, gastos ni notas de crédito. El
    /// máximo por lote es de 50 documentos.
    /// </remarks>
    /// <param name="body">Ids de los documentos y bandera para procesar solo los que no tienen XML.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la descarga en lote.</response>
    [HttpPost("documents/download-xml-pending")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<BatchDownloadPurchaseReceptionXmlResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> DownloadXmlPending(
        [FromBody] BatchDownloadPurchaseReceptionXmlRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new BatchDownloadPurchaseReceptionXmlCommand(body.DocumentIds, body.OnlyMissingXml),
                ct
            )
        );

    /// <summary>
    /// Prepara la compra desde un documento de recepción.
    /// </summary>
    /// <remarks>
    /// Precarga el borrador a partir del XML guardado y de la conciliación persistida. Solo una factura genera
    /// compra (la nota de crédito sigue su propio flujo) y una recepción puede generar compra o gasto, nunca ambos.
    /// Requiere un proveedor con socio y rol activos.
    /// </remarks>
    /// <param name="id">Id de el documento de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Datos para el borrador de compra.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("{id:guid}/create-draft")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<PurchaseDraftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDraft(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CreatePurchaseReceptionDraftCommand(id), ct)
        );

    /// <summary>
    /// Prepara el gasto desde un documento de recepción.
    /// </summary>
    /// <remarks>
    /// Cabecera de solo lectura para precargar el formulario de Nuevo Gasto. Solo facturas, nunca notas de crédito.
    /// No crea ni persiste el documento de gasto: eso ocurre al guardar desde el formulario de Gastos.
    /// </remarks>
    /// <param name="id">Id de el documento de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Datos para el formulario de gasto.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost("{id:guid}/create-expense-draft")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsCreate}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ExpenseReceptionDraftDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateExpenseDraft(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CreateExpenseDraftFromReceptionQuery(id), ct)
        );

    /// <summary>
    /// Vista de solo lectura del XML guardado de un documento de recepción.
    /// </summary>
    /// <remarks>
    /// Nunca descarga, reprocesa ni cambia el estado del documento.
    /// </remarks>
    /// <param name="id">Id de el documento de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Vista del XML.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("documents/{id:guid}/xml-view")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionXmlViewDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetXmlView(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPurchaseReceptionXmlViewQuery(id), ct));

    // ── Item Matching ────────────────────────────────────────────────────

    /// <summary>
    /// Lista las líneas de un documento de recepción con su conciliación de ítems.
    /// </summary>
    /// <param name="id">Id de el documento de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Líneas del documento con su conciliación.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}/lines")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseReceptionLineMatchDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLines(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new FindItemMatchesQuery(id), ct));

    /// <summary>
    /// Obtiene la conciliación de una línea de recepción.
    /// </summary>
    /// <param name="id">Id de la línea de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Conciliación de la línea.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("lines/{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLineMatch(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPurchaseReceptionLineMatchQuery(id), ct));

    /// <summary>
    /// Vincula una línea de recepción con un ítem y una presentación.
    /// </summary>
    /// <remarks>
    /// Aprende la equivalencia Proveedor + Código del proveedor → Ítem + Presentación. El permiso exacto se decide
    /// en el servidor según el comprobante (el endpoint exige <c>purchases.view</c> como acceso al módulo).
    /// </remarks>
    /// <param name="id">Id de la línea de recepción.</param>
    /// <param name="body">Ítem y presentación a vincular.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Línea vinculada.</response>
    /// <response code="403">El usuario no tiene el permiso exacto que exige la conciliación según el comprobante: <c>purchases.update</c> si ya existe una compra activa de esa clave, <c>purchases.create</c> si no.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">La compra del comprobante ya está confirmada: no se modifican las líneas de recepción ni las equivalencias de código de proveedor.</response>
    [HttpPost("lines/{id:guid}/match-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MatchItem(
        Guid id,
        [FromBody] MatchItemRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new MatchItemCommand(id, body.ItemId, body.PackagingLevelId), ct)
        );

    /// <summary>
    /// Desvincula el ítem de una línea de recepción.
    /// </summary>
    /// <remarks>
    /// Desactiva la equivalencia aprendida no principal. El permiso exacto se decide en el servidor según el
    /// comprobante.
    /// </remarks>
    /// <param name="id">Id de la línea de recepción.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Línea desvinculada.</response>
    /// <response code="403">El usuario no tiene el permiso exacto que exige la conciliación según el comprobante: <c>purchases.update</c> si ya existe una compra activa de esa clave, <c>purchases.create</c> si no.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">La compra del comprobante ya está confirmada: no se modifican las líneas de recepción ni las equivalencias de código de proveedor.</response>
    [HttpPost("lines/{id:guid}/unmatch-item")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseReceptionLineMatchDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnmatchItem(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new UnmatchPurchaseReceptionItemCommand(id), ct)
        );

    /// <summary>
    /// Corrige el ítem y la presentación de una línea ya vinculada.
    /// </summary>
    /// <remarks>
    /// Cambia el producto y la presentación y reemplaza la equivalencia Proveedor + Código en una sola transacción
    /// (la anterior queda deshabilitada). Devuelve todas las líneas afectadas del documento. Se rechaza si la
    /// compra ya está confirmada.
    /// </remarks>
    /// <param name="id">Id de la línea de recepción.</param>
    /// <param name="body">Ítem y presentación correctos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Líneas del documento actualizadas.</response>
    /// <response code="403">El usuario no tiene el permiso exacto que exige la conciliación según el comprobante: <c>purchases.update</c> si ya existe una compra activa de esa clave, <c>purchases.create</c> si no.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">La compra del comprobante ya está confirmada: no se modifican las líneas de recepción ni las equivalencias de código de proveedor.</response>
    [HttpPost("lines/{id:guid}/correct-match")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<CorrectReceptionLineMatchResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CorrectMatch(
        Guid id,
        [FromBody] MatchItemRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CorrectReceptionLineMatchCommand(id, body.ItemId, body.PackagingLevelId),
                ct
            )
        );

    /// <summary>
    /// Vincula en lote varias líneas de recepción con ítems existentes.
    /// </summary>
    /// <remarks>
    /// Un lote se valida completo antes de escribir.
    /// </remarks>
    /// <param name="matches">Lista de líneas con el ítem y la presentación a vincular.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la conciliación en lote.</response>
    /// <response code="403">El usuario no tiene el permiso exacto que exige la conciliación según el comprobante: <c>purchases.update</c> si ya existe una compra activa de esa clave, <c>purchases.create</c> si no.</response>
    /// <response code="409">La compra del comprobante ya está confirmada: no se modifican las líneas de recepción ni las equivalencias de código de proveedor.</response>
    [HttpPost("matching/bulk")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<BulkMatchItemsResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BulkMatch(
        [FromBody] IReadOnlyList<BulkMatchItemEntry> matches,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(new BulkMatchItemsCommand(matches), ct));

    /// <summary>
    /// Resuelve en masa las líneas de recepción pendientes.
    /// </summary>
    /// <remarks>
    /// Crea productos nuevos (con sus presentaciones) y/o vincula existentes, aprendiendo Proveedor + Código → Ítem
    /// + Presentación. Todo o nada. Crear productos exige además <c>items.create</c>, validado en Application.
    /// </remarks>
    /// <param name="body">Productos nuevos a crear y líneas a vincular.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la resolución en masa.</response>
    /// <response code="403">El usuario no tiene el permiso exacto que exige la conciliación según el comprobante: <c>purchases.update</c> si ya existe una compra activa de esa clave, <c>purchases.create</c> si no.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">La compra del comprobante ya está confirmada: no se modifican las líneas de recepción ni las equivalencias de código de proveedor.</response>
    [HttpPost("matching/resolve")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ResolvePurchaseReceptionLinesResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveLines(
        [FromBody] ResolvePurchaseReceptionLinesRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ResolvePurchaseReceptionLinesCommand(body.NewItems ?? [], body.Lines ?? []),
                ct
            )
        );
}

public sealed record ResolvePurchaseReceptionLinesRequest(
    IReadOnlyList<ResolveReceptionNewItemInput>? NewItems,
    IReadOnlyList<ResolveReceptionLineInput>? Lines
);

public sealed record MatchItemRequest(Guid ItemId, Guid? PackagingLevelId = null);

/// <summary>FASE 5 PURCHASE-UX — cuerpo de "Actualizar estados".</summary>
public sealed record RefreshPurchaseReceptionStatusRequest(IReadOnlyList<Guid> DocumentIds);

public sealed record BatchDownloadPurchaseReceptionXmlRequest(
    IReadOnlyList<Guid> DocumentIds,
    bool OnlyMissingXml = true
);

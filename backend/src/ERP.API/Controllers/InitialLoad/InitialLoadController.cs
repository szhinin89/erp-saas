using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.API.Uploads;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.DownloadImportTemplate;
using ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchHistory;
using ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchStatus;
using ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;
using ERP.Application.Modules.InitialLoad.UseCases.PreviewImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.InitialLoad.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.InitialLoad;

/// <summary>
/// Carga inicial: importación por lotes de Clientes, Proveedores, Ítems, Inventario inicial, CxC inicial y
/// CxP inicial desde Excel (.xlsx), fecha de apertura de saldos y asiento contable de apertura.
/// </summary>
[AppFeature(
    "Carga inicial",
    $"perm:{InitialLoadPermissions.View}",
    "upload_file",
    "/initial-load",
    null,
    95
)]
[ApiController]
[Route("api/v1/initial-load")]
[Authorize]
[Produces("application/json")]
public sealed class InitialLoadController : ControllerBase
{
    private readonly IMediator _mediator;

    public InitialLoadController(IMediator mediator) => _mediator = mediator;

    /// <summary>Crea un lote de importación vacío (estado <c>Draft</c>) para un tipo de dato de la Carga Inicial.</summary>
    /// <remarks>
    /// Solo se admiten los tipos con procesador registrado: <c>Customers</c>, <c>Suppliers</c>, <c>Items</c>,
    /// <c>InitialStock</c>, <c>InitialReceivables</c> e <c>InitialPayables</c>; <c>Prices</c> existe en el enum pero no
    /// tiene procesador y se rechaza. <c>label</c> admite hasta 200 caracteres. <c>autoCreateCatalogValues</c> se guarda en
    /// el lote y la confirmación de ítems lo aplica para crear valores de catálogo faltantes. Los tipos de saldos
    /// (<c>InitialStock</c>, <c>InitialReceivables</c>, <c>InitialPayables</c>) no abren lote una vez cerrada la Carga Inicial
    /// (<c>INITIAL_LOAD_CLOSED</c>).
    /// </remarks>
    /// <param name="command">Tipo de importación, etiqueta opcional y bandera de creación automática de catálogos.</param>
    /// <response code="201">Lote creado en estado <c>Draft</c>.</response>
    /// <response code="400">La Carga Inicial ya está cerrada y el tipo es de saldos (<c>INITIAL_LOAD_CLOSED</c>).</response>
    /// <response code="422">Tipo de importación fuera del enum o sin procesador disponible, o etiqueta de más de 200 caracteres.</response>
    [HttpPost("batches")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ImportBatchDto>),
        StatusCodes.Status201Created
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateImportBatchCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Adjunta el archivo Excel (.xlsx) al lote y lo deja en estado <c>Uploaded</c>.</summary>
    /// <remarks>
    /// Solo acepta lotes en <c>Draft</c> o <c>Uploaded</c> (se puede reemplazar el archivo antes de validar). Máximo 10 MB y
    /// extensión <c>.xlsx</c>. Si el lote rechaza el adjunto por su estado, el archivo ya guardado se elimina para no dejar
    /// huérfanos. En los tipos de saldos no se sube archivo una vez cerrada la Carga Inicial.
    /// </remarks>
    /// <param name="id">Id del lote de importación.</param>
    /// <param name="file">Archivo <c>.xlsx</c> (multipart/form-data, campo <c>file</c>).</param>
    /// <response code="200">Archivo adjuntado; devuelve el lote actualizado.</response>
    /// <response code="400">No se envió archivo (o está vacío), o la Carga Inicial está cerrada para un lote de saldos (<c>INITIAL_LOAD_CLOSED</c>).</response>
    /// <response code="404">El lote no existe en la empresa activa.</response>
    /// <response code="422">Archivo no <c>.xlsx</c>, vacío o mayor de 10 MB; no se pudo guardar el archivo; o el estado del lote no admite adjuntos.</response>
    [HttpPost("batches/{id:guid}/upload")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo.");

        await using var upload = await BufferedFormFile.CreateAsync(file, ct);
        var result = await _mediator.Send(new UploadImportFileCommand(id, upload.Content), ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Valida las filas del archivo del lote y registra los errores y advertencias por fila.</summary>
    /// <remarks>
    /// Solo válido para lotes en <c>Uploaded</c> o <c>Validated</c>; se puede repetir y cada corrida reemplaza el resultado
    /// anterior (el staging previo se conserva si la validación falla o se cancela). Los errores de fila no hacen fallar el
    /// lote: quedan en los contadores y en la vista previa (<c>error</c> bloquea la fila; <c>warning</c> no). Un archivo ilegible
    /// o un error inesperado deja el lote en <c>Failed</c>. Tras el cierre de la Carga Inicial no se validan lotes de saldos.
    /// </remarks>
    /// <param name="id">Id del lote de importación.</param>
    /// <response code="200">Validación terminada (<c>Validated</c>); devuelve el lote con sus contadores.</response>
    /// <response code="400">La Carga Inicial está cerrada para un lote de saldos (<c>INITIAL_LOAD_CLOSED</c>) o falló la lectura de ítems (error inesperado).</response>
    /// <response code="404">El lote no existe en la empresa activa.</response>
    /// <response code="422">Estado del lote no válido, sin archivo, archivo no disponible o ilegible, sin procesador, o alcance del staging no válido.</response>
    [HttpPost("batches/{id:guid}/validate")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ValidateImportBatchCommand(id), ct));

    /// <summary>Lista paginada de las filas del lote con sus datos y los problemas detectados en la validación.</summary>
    /// <remarks>
    /// Solo lectura. <c>pageSize</c> fuera de 1–200 se sustituye por 50 y <c>page</c> menor a 1 por 1. Un lote inexistente
    /// devuelve una página vacía (no hay 404).
    /// </remarks>
    /// <param name="id">Id del lote de importación.</param>
    /// <param name="page">Número de página (desde 1; por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (1–200; por defecto 50).</param>
    /// <param name="onlyWithBlockingIssue">Si es <c>true</c>, solo filas con algún error que bloquea su confirmación.</param>
    /// <response code="200">Página de filas del lote.</response>
    [HttpGet("batches/{id:guid}/preview")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PagedResult<ImportBatchRowPreviewDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? onlyWithBlockingIssue = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new PreviewImportBatchQuery(id, page, pageSize, onlyWithBlockingIssue),
                ct
            )
        );

    /// <summary>Confirma el lote validado e importa sus filas al sistema (clientes, proveedores, ítems o saldos iniciales).</summary>
    /// <remarks>
    /// Solo desde <c>Validated</c> y sin filas con error: Clientes, Proveedores, Ítems, Inventario inicial, CxC inicial y CxP
    /// inicial se confirman todo-o-nada en una única transacción con bloqueo del lote. Reintentar sobre un lote ya
    /// <c>Completed</c> devuelve el mismo resultado sin volver a importar. Un error inesperado deja el lote en <c>Failed</c>.
    /// La confirmación operativa es independiente del asiento contable de apertura (ver <c>opening-posting</c>). Tras el cierre
    /// de la Carga Inicial no se confirman lotes de saldos.
    /// </remarks>
    /// <param name="id">Id del lote de importación.</param>
    /// <response code="200">Lote confirmado; devuelve estado y filas importadas.</response>
    /// <response code="400">La Carga Inicial está cerrada para un lote de saldos (<c>INITIAL_LOAD_CLOSED</c>), o falló la confirmación de forma inesperada.</response>
    /// <response code="404">El lote no existe en la empresa activa.</response>
    /// <response code="422">El lote no está validado, tiene errores en filas o incumple una regla de negocio al importar.</response>
    [HttpPost("batches/{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ImportBatchConfirmResultDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ConfirmImportBatchCommand(id), ct));

    /// <summary>
    /// IL-7B — contabiliza o reintenta el asiento de apertura del lote (también lotes históricos).
    /// Exige ambos permisos existentes: confirmar Carga Inicial y crear en Contabilidad.
    /// </summary>
    /// <remarks>
    /// Corre en una transacción con bloqueo del lote. Es idempotente: si el lote ya tiene su asiento contabilizado devuelve
    /// ese resultado con <c>alreadyPosted = true</c> sin generar otro. Un fallo contable (fecha o monto de apertura
    /// ausentes, política contable, período cerrado, origen cambiado, fallo del motor) se registra en el lote como
    /// <c>Failed</c> con su código y se puede reintentar; nunca revierte la carga operativa ya confirmada. Tras el cierre de la
    /// Carga Inicial no se contabiliza ni se reintenta.
    /// </remarks>
    /// <param name="id">Id del lote de importación a contabilizar.</param>
    /// <response code="200">Asiento de apertura contabilizado (o ya contabilizado: <c>alreadyPosted</c>).</response>
    /// <response code="400">Rechazo contable con código propio: Carga Inicial cerrada (<c>INITIAL_LOAD_CLOSED</c>), política contable, <c>OPENING_SOURCE_CHANGED</c>, falla del motor de contabilización, error inesperado.</response>
    /// <response code="404">El lote no existe en la empresa activa (<c>BATCH_NOT_FOUND</c>).</response>
    [HttpPost("batches/{id:guid}/opening-posting")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningBalancePostingDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostOpeningBalance(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new PostOpeningBalanceCommand(id), ct));

    /// <summary>
    /// IL-8A — publica (o reintenta) el único ASI de apertura de la empresa: líneas Debe/Haber que
    /// dejan la cuenta puente "Saldos de apertura" en 0, a la fecha de apertura. No es un editor
    /// contable general ni expone reverso. Exige confirmar Carga Inicial y crear en Contabilidad.
    /// </summary>
    /// <remarks>
    /// Entre 2 y 500 líneas; cada línea con cuenta contable y un monto mayor a cero solo en Debe o solo en Haber, con máximo
    /// 2 decimales, y descripción de hasta 500 caracteres. Requiere la fecha de apertura definida y deja la cuenta puente en
    /// cero. Es idempotente: si ya hay un asiento publicado devuelve ese resultado con <c>alreadyPosted = true</c>. Un fallo
    /// (fecha ausente, regla de apertura con líneas fijas, cuentas de control, puente sin compensar, período cerrado o fallo del
    /// motor) se registra con su código y se puede reintentar. Tras el cierre de la Carga Inicial no se publica ni se reintenta.
    /// </remarks>
    /// <param name="command">Líneas Debe/Haber del asiento de apertura.</param>
    /// <response code="200">Asiento de apertura publicado (o ya publicado: <c>alreadyPosted</c>).</response>
    /// <response code="400">Rechazo contable con código propio (<c>OPENING_DATE_MISSING</c>, <c>OPENING_BRIDGE_NOT_CLEARED</c>, <c>OPENING_CONTROL_*</c>, Carga Inicial cerrada, política contable, falla del motor, error inesperado).</response>
    /// <response code="422">Cuerpo inválido: número de líneas, cuenta ausente, montos o descripción fuera de regla.</response>
    [HttpPost("opening-journal-entry")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningJournalEntryPostingDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishOpeningJournalEntry(
        [FromBody] PublishOpeningJournalEntryCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// IL-8B — reversa por completo el ASI de apertura VIGENTE y publicado (motivo obligatorio) y lo
    /// deja como historial; después se publica una versión nueva con el endpoint anterior. No es un
    /// reverso contable genérico. Exige confirmar Carga Inicial y eliminar en Contabilidad.
    /// </summary>
    /// <remarks>
    /// Solo se reversa la versión vigente y publicada; el motivo es obligatorio. Es idempotente: reintentar sobre una versión ya
    /// reversada devuelve el mismo resultado. Solo se corrige mientras todos los períodos contables desde la fecha de apertura
    /// hasta hoy estén abiertos, y no después del cierre de la Carga Inicial.
    /// </remarks>
    /// <param name="postingId">Id de la versión publicada del asiento de apertura.</param>
    /// <param name="request">Motivo del reverso.</param>
    /// <response code="200">Asiento de apertura reversado.</response>
    /// <response code="400">Rechazo contable con código propio: <c>OPENING_ASI_NOT_CURRENT</c>, <c>OPENING_ASI_NOT_POSTED</c>, <c>OPENING_ASI_JOURNAL_MISMATCH</c>, período cerrado, Carga Inicial cerrada, error inesperado.</response>
    /// <response code="404">La versión del asiento de apertura no existe en la empresa activa.</response>
    /// <response code="422">Motivo vacío o de más caracteres de los permitidos.</response>
    [HttpPost("opening-journal-entry/{postingId:guid}/reverse")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Delete}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningJournalEntryReversalDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReverseOpeningJournalEntry(
        Guid postingId,
        [FromBody] ReverseOpeningJournalEntryRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ReverseOpeningJournalEntryCommand(postingId, request.Reason), ct)
        );

    /// <summary>
    /// IL-7C — conciliación de apertura (solo lectura): por lote y por tipo, saldo operativo
    /// confirmado contra mayor al corte, cuenta puente y bloqueos de cierre (IL-8). Muestra montos
    /// contables: exige ver Carga Inicial y ver Contabilidad.
    /// </summary>
    /// <remarks>
    /// No modifica datos. La respuesta incluye los bloqueos (<c>blockers</c>) que impedirían cerrar la Carga Inicial, entre ellos
    /// fecha de apertura ausente, lotes en curso, asiento de apertura faltante, pendiente, fallido o reversado sin reemplazo, y
    /// cuenta puente distinta de cero.
    /// </remarks>
    /// <response code="200">Conciliación de apertura por lote y por tipo, con cuenta puente, bloqueos y estado de cierre.</response>
    [HttpGet("opening-reconciliation")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningBalanceReconciliationDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetOpeningReconciliation(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetOpeningBalanceReconciliationQuery(), ct));

    /// <summary>
    /// IL-8E — cierre DEFINITIVO e irreversible de la Carga Inicial: solo sin blockers de la
    /// conciliación de apertura. Después ninguna operación modifica la apertura. Exige confirmar
    /// Carga Inicial y crear en Contabilidad (mismos permisos que publicar el ASI).
    /// </summary>
    /// <remarks>
    /// Requiere un asiento de apertura vigente publicado (siempre, aunque no haya lotes) y cuenta puente en cero; los bloqueos
    /// operativos de la conciliación (fecha de apertura ausente, lote en curso) impiden el cierre. Es idempotente: si la empresa
    /// ya está cerrada devuelve el cierre existente con <c>alreadyClosed = true</c>. No borra ni modifica historial. Después del
    /// cierre se rechazan los lotes de saldos, la contabilización, publicación y reverso del asiento de apertura y la fecha de
    /// apertura.
    /// </remarks>
    /// <response code="200">Carga Inicial cerrada (o ya cerrada: <c>alreadyClosed</c>).</response>
    /// <response code="400">Hay bloqueos (<c>INITIAL_LOAD_CLOSE_BLOCKED</c>) o falló la conciliación previa al cierre.</response>
    /// <response code="404">La empresa no existe.</response>
    [HttpPost("close")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<InitialLoadClosureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseInitialLoad(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CloseInitialLoadCommand(), ct));

    /// <summary>Cancela el lote de importación (estado <c>Cancelled</c>).</summary>
    /// <remarks>
    /// Solo desde <c>Draft</c>, <c>Uploaded</c> o <c>Validated</c>; un lote en confirmación o ya confirmado no se cancela. Se
    /// ejecuta en una transacción con bloqueo del lote para no pisar una confirmación en curso. No elimina datos ya importados.
    /// </remarks>
    /// <param name="id">Id del lote de importación.</param>
    /// <response code="200">Lote cancelado (<c>true</c>).</response>
    /// <response code="404">El lote no existe en la empresa activa.</response>
    /// <response code="422">El estado del lote no admite cancelación, o el alcance del staging no es válido.</response>
    [HttpPost("batches/{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CancelImportBatchCommand(id), ct));

    /// <summary>Obtiene el estado y los contadores de un lote de importación.</summary>
    /// <param name="id">Id del lote de importación.</param>
    /// <response code="200">Datos del lote (estado, archivos, filas totales, válidas y con error).</response>
    /// <response code="404">El lote no existe en la empresa activa.</response>
    [HttpGet("batches/{id:guid}", Name = nameof(GetBatch))]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatch(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetImportBatchStatusQuery(id), ct));

    /// <summary>Historial paginado de lotes de importación de la empresa activa.</summary>
    /// <remarks>
    /// Solo lectura. <c>pageSize</c> fuera de 1–100 se sustituye por 20 y <c>page</c> menor a 1 por 1.
    /// </remarks>
    /// <param name="importType">Filtra por tipo de importación (opcional).</param>
    /// <param name="page">Número de página (desde 1; por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (1–100; por defecto 20).</param>
    /// <response code="200">Página de lotes.</response>
    [HttpGet("batches")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PagedResult<ImportBatchDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetHistory(
        [FromQuery] ImportType? importType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetImportBatchHistoryQuery(importType, page, pageSize), ct)
        );

    /// <summary>Consulta la fecha de apertura de saldos de la empresa activa y sus restricciones de cambio.</summary>
    /// <remarks>IL-5A — Configuración → Implementación. Solo lectura.</remarks>
    /// <response code="200">Fecha de apertura (puede ser nula si no se definió) y motivos que impiden modificarla.</response>
    /// <response code="404">La empresa no existe.</response>
    // IL-5A — fecha de apertura de saldos de la empresa activa (Configuración → Implementación).
    [HttpGet("opening-balance-date")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<OpeningBalanceDateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOpeningBalanceDate(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetOpeningBalanceDateQuery(), ct));

    /// <summary>Define o modifica la fecha de apertura de saldos de la empresa activa.</summary>
    /// <remarks>
    /// Se ejecuta en una transacción con bloqueo de la apertura de la empresa. La fecha es obligatoria (formato
    /// <c>AAAA-MM-DD</c>). Las reglas de negocio pueden impedir el cambio: una fecha de apertura ya fijada por un asiento
    /// publicado es inmutable y tampoco se modifica tras el cierre de la Carga Inicial; el motivo se devuelve en el error.
    /// </remarks>
    /// <param name="command">Fecha de apertura de saldos.</param>
    /// <response code="200">Fecha de apertura guardada.</response>
    /// <response code="404">La empresa no existe.</response>
    /// <response code="422">Fecha ausente o regla de apertura incumplida (fecha bloqueada o inválida).</response>
    [HttpPut("opening-balance-date")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<OpeningBalanceDateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetOpeningBalanceDate(
        [FromBody] SetOpeningBalanceDateCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Descarga la plantilla Excel (.xlsx) del tipo de importación indicado.</summary>
    /// <param name="importType">Tipo de importación (<c>Customers</c>, <c>Suppliers</c>, <c>Items</c>, <c>InitialStock</c>, <c>InitialReceivables</c> o <c>InitialPayables</c>).</param>
    /// <response code="200">Archivo <c>.xlsx</c> con las columnas esperadas por el importador (no es un envelope JSON).</response>
    /// <response code="422">Tipo de importación desconocido o sin plantilla disponible (<c>Prices</c>).</response>
    [HttpGet("templates/{importType}")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DownloadTemplate(ImportType importType, CancellationToken ct)
    {
        var result = await _mediator.Send(new DownloadImportTemplateQuery(importType), ct);
        if (!result.IsSuccess)
            return this.ToOkOrBadRequest(result);

        var file = result.Value!;
        return File(file.Content, file.ContentType, file.FileName);
    }
}

/// <summary>IL-8B — cuerpo del reverso del ASI de apertura: solo el motivo (la versión va en la ruta).</summary>
public sealed record ReverseOpeningJournalEntryRequest(string Reason);

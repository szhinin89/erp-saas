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

/// <summary>Carga Inicial (INITIAL-LOAD-ARCH-01) — solo Clientes disponible en esta entrega.</summary>
[AppFeature(
    "Carga Inicial",
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

    [HttpPost("batches")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ImportBatchDto>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateImportBatchCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPost("batches/{id:guid}/upload")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo.");

        await using var upload = await BufferedFormFile.CreateAsync(file, ct);
        var result = await _mediator.Send(new UploadImportFileCommand(id, upload.Content), ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPost("batches/{id:guid}/validate")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ValidateImportBatchCommand(id), ct));

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

    [HttpPost("batches/{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<ImportBatchConfirmResultDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new ConfirmImportBatchCommand(id), ct));

    /// <summary>
    /// IL-7B — contabiliza o reintenta el asiento de apertura del lote (también lotes históricos).
    /// Exige ambos permisos existentes: confirmar Carga Inicial y crear en Contabilidad.
    /// </summary>
    [HttpPost("batches/{id:guid}/opening-posting")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningBalancePostingDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> PostOpeningBalance(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new PostOpeningBalanceCommand(id), ct));

    /// <summary>
    /// IL-8A — publica (o reintenta) el único ASI de apertura de la empresa: líneas Debe/Haber que
    /// dejan la cuenta puente "Saldos de apertura" en 0, a la fecha de apertura. No es un editor
    /// contable general ni expone reverso. Exige confirmar Carga Inicial y crear en Contabilidad.
    /// </summary>
    [HttpPost("opening-journal-entry")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Create}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningJournalEntryPostingDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> PublishOpeningJournalEntry(
        [FromBody] PublishOpeningJournalEntryCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// IL-8B — reversa por completo el ASI de apertura VIGENTE y publicado (motivo obligatorio) y lo
    /// deja como historial; después se publica una versión nueva con el endpoint anterior. No es un
    /// reverso contable genérico. Exige confirmar Carga Inicial y eliminar en Contabilidad.
    /// </summary>
    [HttpPost("opening-journal-entry/{postingId:guid}/reverse")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.Delete}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningJournalEntryReversalDto>),
        StatusCodes.Status200OK
    )]
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
    [HttpGet("opening-reconciliation")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [Authorize(Policy = $"perm:{AccountingPermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<OpeningBalanceReconciliationDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetOpeningReconciliation(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetOpeningBalanceReconciliationQuery(), ct));

    [HttpPost("batches/{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Create}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CancelImportBatchCommand(id), ct));

    [HttpGet("batches/{id:guid}", Name = nameof(GetBatch))]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatch(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetImportBatchStatusQuery(id), ct));

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

    // IL-5A — fecha de apertura de saldos de la empresa activa (Configuración → Implementación).
    [HttpGet("opening-balance-date")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<OpeningBalanceDateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpeningBalanceDate(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetOpeningBalanceDateQuery(), ct));

    [HttpPut("opening-balance-date")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.Confirm}")]
    [ProducesResponseType(typeof(Contracts.ApiResponse<OpeningBalanceDateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetOpeningBalanceDate(
        [FromBody] SetOpeningBalanceDateCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    [HttpGet("templates/{importType}")]
    [Authorize(Policy = $"perm:{InitialLoadPermissions.View}")]
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

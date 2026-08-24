using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.UseCases.CancelImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.DownloadImportTemplate;
using ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchHistory;
using ERP.Application.Modules.InitialLoad.UseCases.GetImportBatchStatus;
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
    [ProducesResponseType(typeof(Contracts.ApiResponse<ImportBatchDto>), StatusCodes.Status201Created)]
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

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var content = new MediaUploadContent(stream, file.FileName, file.ContentType, file.Length);
        var result = await _mediator.Send(new UploadImportFileCommand(id, content), ct);
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

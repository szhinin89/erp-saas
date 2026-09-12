using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;

/// <summary>
/// Orquesta la descarga en lote reutilizando <see cref="DownloadPurchaseReceptionXmlCommand"/> por
/// documento — nunca reimplementa la consulta SOAP al SRI, el parseo del XML ni la persistencia
/// (eso vive únicamente en <c>DownloadPurchaseReceptionXmlHandler</c>).
///
/// Cada documento se procesa en su propio <see cref="IServiceScope"/> (propio <c>DbContext</c>,
/// propio <see cref="IMediator"/>) porque el <c>DbContext</c> scoped de ASP.NET Core no admite uso
/// concurrente: correr los N documentos sobre el mismo scope del request rompería en cuanto dos
/// descargas se solaparan. <see cref="ICurrentTenant"/>/<c>ICurrentCompany</c>/<c>ICurrentBranch</c>
/// leen del <c>HttpContext</c> ambiente (no del scope), así que siguen resolviendo el tenant/
/// empresa/sucursal correctos en cada scope hijo. La concurrencia real (llamadas SOAP al SRI, la
/// parte lenta) se acota con <see cref="SemaphoreSlim"/> a <see cref="MaxConcurrency"/> descargas
/// simultáneas para no saturar el servicio del SRI.
/// </summary>
public sealed class BatchDownloadPurchaseReceptionXmlHandler
    : IRequestHandler<
        BatchDownloadPurchaseReceptionXmlCommand,
        Result<BatchDownloadPurchaseReceptionXmlResultDto>
    >
{
    private const int MaxConcurrency = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchDownloadPurchaseReceptionXmlHandler> _logger;

    public BatchDownloadPurchaseReceptionXmlHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<BatchDownloadPurchaseReceptionXmlHandler> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Result<BatchDownloadPurchaseReceptionXmlResultDto>> Handle(
        BatchDownloadPurchaseReceptionXmlCommand request,
        CancellationToken cancellationToken
    )
    {
        var results = new BatchDownloadPurchaseReceptionXmlItemResult[request.DocumentIds.Count];
        using var semaphore = new SemaphoreSlim(MaxConcurrency);

        var tasks = request.DocumentIds.Select(
            async (documentId, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    results[index] = await ProcessOneAsync(documentId, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        );

        await Task.WhenAll(tasks);

        var items = results.ToList();
        var dto = new BatchDownloadPurchaseReceptionXmlResultDto(
            Total: items.Count,
            Processed: items.Count,
            Downloaded: items.Count(i => i.Status == "Downloaded"),
            Skipped: items.Count(i => i.Status == "SkippedAlreadyHasXml"),
            Failed: items.Count(i =>
                i.Status is "SriError" or "ValidationError" or "Error"
            ),
            Items: items
        );

        return Result<BatchDownloadPurchaseReceptionXmlResultDto>.Success(dto);
    }

    private async Task<BatchDownloadPurchaseReceptionXmlItemResult> ProcessOneAsync(
        Guid documentId,
        CancellationToken cancellationToken
    )
    {
        using var scope = _scopeFactory.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var documentRepo = scope.ServiceProvider.GetRequiredService<IPurchaseReceptionDocumentRepository>();

        try
        {
            var document = await documentRepo.GetByIdAsync(tenant.TenantId, documentId, cancellationToken);
            if (document is null)
            {
                return new BatchDownloadPurchaseReceptionXmlItemResult(
                    documentId,
                    "Error",
                    "El documento de recepción no existe."
                );
            }

            if (document.XmlContent is not null)
            {
                var skippedSnapshot = await BuildSnapshotAsync(scope, tenant.TenantId, document, cancellationToken);
                return skippedSnapshot with
                {
                    Status = "SkippedAlreadyHasXml",
                    Message = "El documento ya tenía XML.",
                };
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(
                new DownloadPurchaseReceptionXmlCommand(documentId),
                cancellationToken
            );

            if (result.IsSuccess)
            {
                // Misma instancia de PurchaseReceptionDocument que mutó el handler individual (mismo
                // scope => mismo DbContext => mismo change tracker): document ya refleja el estado
                // Verified/XmlContent recién persistido, sin necesidad de recargarlo.
                var downloadedSnapshot = await BuildSnapshotAsync(scope, tenant.TenantId, document, cancellationToken);
                return downloadedSnapshot with
                {
                    Status = "Downloaded",
                    Message = "XML descargado correctamente.",
                };
            }

            var status = result.Code switch
            {
                ApiResponseCodes.Common.SriCommunicationError => "SriError",
                ApiResponseCodes.Common.ValidationError => "ValidationError",
                ApiResponseCodes.Common.NotFound => "Error",
                _ => "Error",
            };

            var failedSnapshot = await BuildSnapshotAsync(scope, tenant.TenantId, document, cancellationToken);
            return failedSnapshot with
            {
                Status = status,
                Message = result.Error ?? "No se pudo descargar el XML.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fallo al descargar el XML en lote del documento de recepción {DocumentId}",
                documentId
            );
            return new BatchDownloadPurchaseReceptionXmlItemResult(
                documentId,
                "Error",
                "Ocurrió un error inesperado al descargar el XML."
            );
        }
    }

    /// <summary>
    /// Resumen mínimo para refrescar los badges de la fila de Recepción (nunca XML/líneas) — el
    /// mismo dato que ya calcula <c>DownloadPurchaseReceptionXmlHandler</c> para su propio DTO
    /// (compra/gasto por AccessKey) más el estado de NC, calculado aquí porque el DTO individual no
    /// lo expone. <see cref="BatchDownloadPurchaseReceptionXmlItemResult.Status"/>/<c>Message</c>
    /// quedan vacíos — el llamador los completa con <c>with</c> según el resultado de esa fila.
    /// </summary>
    private static async Task<BatchDownloadPurchaseReceptionXmlItemResult> BuildSnapshotAsync(
        IServiceScope scope,
        Guid tenantId,
        PurchaseReceptionDocument document,
        CancellationToken cancellationToken
    )
    {
        var purchaseRepo = scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceRepository>();
        var expenseRepo = scope.ServiceProvider.GetRequiredService<IExpenseDocumentRepository>();

        var existingPurchase = await purchaseRepo.GetByAccessKeyAsync(
            tenantId,
            document.AccessKey,
            cancellationToken
        );
        var expenseId = await expenseRepo.GetActiveIdByAccessKeyAsync(
            tenantId,
            document.AccessKey,
            cancellationToken
        );

        Guid? creditNoteId = null;
        Guid? cancelledCreditNoteId = null;
        if (document.SourceDocType == PurchaseReceptionSourceDocType.CreditNote)
        {
            var creditNoteRepo = scope.ServiceProvider.GetRequiredService<IPurchaseCreditNoteRepository>();
            creditNoteId = await creditNoteRepo.GetIdByReceptionDocumentIdAsync(
                tenantId,
                document.Id,
                cancellationToken
            );
            if (creditNoteId is null)
                cancelledCreditNoteId = await creditNoteRepo.GetLatestCancelledIdByReceptionDocumentIdAsync(
                    tenantId,
                    document.Id,
                    cancellationToken
                );
        }

        return new BatchDownloadPurchaseReceptionXmlItemResult(
            document.Id,
            Status: string.Empty,
            Message: string.Empty,
            DocumentStatus: PurchaseReceptionMapper.ToDocumentStatusCode(document.Status),
            ProcessingStatus: PurchaseReceptionMapper.ToProcessingStatusCode(document.ProcessingStatus),
            HasXml: document.XmlContent is not null,
            PurchaseExists: existingPurchase is not null,
            PurchaseId: existingPurchase?.Id,
            ExpenseExists: expenseId is not null,
            ExpenseId: expenseId,
            CreditNoteExists: creditNoteId is not null,
            CreditNoteId: creditNoteId,
            CancelledCreditNoteId: cancelledCreditNoteId
        );
    }
}

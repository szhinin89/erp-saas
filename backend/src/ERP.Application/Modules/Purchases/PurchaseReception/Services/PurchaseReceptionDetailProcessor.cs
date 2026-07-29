using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Services;

/// <summary>
/// Resultado de interpretar el XML de un comprobante y resolver Item Matching sobre sus líneas —
/// compartido entre <c>DownloadPurchaseReceptionXmlHandler</c> (primera descarga) y
/// <c>ReprocessPurchaseReceptionXmlHandler</c> (reintento sobre el XML ya guardado, sin volver a
/// consultar el SRI). Una sola implementación de la lógica de Item Matching — nunca se duplica.
/// </summary>
public sealed record PurchaseReceptionDetailProcessingResult(
    IReadOnlyList<PurchaseReceptionLine> Lines,
    PurchaseReceptionProcessingOutcome Processing,
    string? DocTypeCode,
    string? SriPaymentMethodCode
);

public interface IPurchaseReceptionDetailProcessor
{
    Task<PurchaseReceptionDetailProcessingResult> ProcessAsync(
        Guid documentId,
        Guid tenantId,
        Guid? supplierId,
        string xmlContent,
        CancellationToken ct
    );
}

/// <summary>
/// Única implementación de "XML -> líneas persistibles + Item Matching resuelto". Nunca crea Items
/// ni decide impuestos por su cuenta — solo interpreta lo que el XML ya trae (ver
/// <see cref="IPurchaseXmlDraftParser"/>) y resuelve conciliación automática por código de
/// proveedor exacto (ver <see cref="IItemRepository.FindItemIdBySupplierCodeAsync"/>) o sugerencias
/// (ver <see cref="IItemMatchFinder"/>).
/// </summary>
public sealed class PurchaseReceptionDetailProcessor : IPurchaseReceptionDetailProcessor
{
    private readonly IPurchaseXmlDraftParser _parser;
    private readonly IItemRepository _itemRepo;
    private readonly IItemMatchFinder _matchFinder;
    private readonly ILogger<PurchaseReceptionDetailProcessor> _logger;

    public PurchaseReceptionDetailProcessor(
        IPurchaseXmlDraftParser parser,
        IItemRepository itemRepo,
        IItemMatchFinder matchFinder,
        ILogger<PurchaseReceptionDetailProcessor> logger
    )
    {
        _parser = parser;
        _itemRepo = itemRepo;
        _matchFinder = matchFinder;
        _logger = logger;
    }

    public async Task<PurchaseReceptionDetailProcessingResult> ProcessAsync(
        Guid documentId,
        Guid tenantId,
        Guid? supplierId,
        string xmlContent,
        CancellationToken ct
    )
    {
        var lines = new List<PurchaseReceptionLine>();
        var noteMessages = new List<string>();
        var parseResult = _parser.Parse(xmlContent);
        var linesDetected = 0;

        if (parseResult.IsSuccess)
        {
            linesDetected = parseResult.Value!.Lines.Count + parseResult.Value.LineErrors.Count;

            foreach (var lineError in parseResult.Value.LineErrors)
            {
                noteMessages.Add(
                    $"Línea {lineError.LineIndex} ({lineError.SupplierCode ?? lineError.Description ?? "sin identificar"}): {lineError.Reason} — línea omitida."
                );
            }

            foreach (var parsedLine in parseResult.Value.Lines)
            {
                Guid? itemId = null;
                var matchStatus = ItemMatchStatus.Pending;

                // Resolución automática — solo por código de proveedor exacto (sin intervención del usuario).
                if (supplierId is { } sid && !string.IsNullOrWhiteSpace(parsedLine.SupplierCode))
                {
                    itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(
                        sid,
                        parsedLine.SupplierCode,
                        tenantId,
                        ct
                    );
                    if (itemId is not null)
                        matchStatus = ItemMatchStatus.AutoMatched;
                }

                try
                {
                    var line = PurchaseReceptionLine.Create(
                        documentId,
                        tenantId,
                        parsedLine.Description,
                        parsedLine.Quantity,
                        parsedLine.UnitPrice,
                        parsedLine.VatCode,
                        parsedLine.TaxCode,
                        parsedLine.VatPercentage,
                        parsedLine.TaxValue,
                        parsedLine.DiscountPct,
                        parsedLine.Discount,
                        parsedLine.LineSubtotal,
                        parsedLine.TotalLine,
                        parsedLine.IceCode,
                        parsedLine.IceValue,
                        parsedLine.SupplierCode,
                        parsedLine.SupplierAuxCode,
                        itemId,
                        matchStatus
                    );

                    // Si no hubo código exacto pero el motor de matching ya encuentra candidatos por
                    // descripción/similitud, la línea queda marcada como "requiere revisión" en vez de
                    // "pendiente" — distingue en la UI qué líneas tienen sugerencias de las que no.
                    if (matchStatus == ItemMatchStatus.Pending)
                    {
                        var suggestions = await _matchFinder.FindCandidatesAsync(
                            tenantId,
                            supplierId,
                            parsedLine.SupplierCode,
                            parsedLine.SupplierAuxCode,
                            parsedLine.Description,
                            maxResults: 1,
                            ct
                        );
                        if (suggestions.Count > 0)
                            line.MarkNeedsReview();
                    }

                    lines.Add(line);
                }
                catch (ArgumentException ex)
                {
                    // Una línea individual inválida (p. ej. cantidad cero por un detalle informativo del
                    // comprobante) no debe bloquear la verificación de todo el documento — se omite y se
                    // registra tanto en el log como en ProcessingNotes (soporte técnico y usuario).
                    _logger.LogWarning(
                        ex,
                        "Línea de detalle omitida al procesar el documento de recepción {DocumentId}: {Reason}",
                        documentId,
                        ex.Message
                    );
                    noteMessages.Add(
                        $"Línea con proveedor {parsedLine.SupplierCode ?? "sin código"} ({parsedLine.Description}): {ex.Message} — línea omitida."
                    );
                }
            }
        }
        else
        {
            _logger.LogWarning(
                "El XML del documento de recepción {DocumentId} no se pudo interpretar: {Reason}",
                documentId,
                parseResult.Error
            );
            noteMessages.Add(
                parseResult.Error ?? "No se pudo interpretar la cabecera del comprobante."
            );
        }

        var processingStatus =
            lines.Count == 0 ? PurchaseReceptionProcessingStatus.Failed
            : noteMessages.Count > 0 ? PurchaseReceptionProcessingStatus.ProcessedWithWarnings
            : PurchaseReceptionProcessingStatus.Processed;

        var processing = new PurchaseReceptionProcessingOutcome(
            processingStatus,
            linesDetected,
            lines.Count,
            noteMessages.Count > 0 ? string.Join(" | ", noteMessages) : null
        );

        return new PurchaseReceptionDetailProcessingResult(
            lines,
            processing,
            parseResult.IsSuccess ? parseResult.Value!.DocTypeCode : null,
            parseResult.IsSuccess ? parseResult.Value!.SriPaymentMethodCode : null
        );
    }
}

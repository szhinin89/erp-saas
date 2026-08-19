using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using DraftMapper = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraftMapper;
using DraftModel = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraft;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;

/// <summary>
/// Arma el borrador de compra desde el <see cref="ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument"/>
/// ya verificado. Cabecera SRI y líneas (incl. Item Matching ya resuelto: <c>ItemId</c>/<c>MatchStatus</c>)
/// fueron persistidas una única vez al descargar el XML (<c>DownloadPurchaseReceptionXmlHandler</c>).
/// El <c>XmlContent</c> guardado es la fuente documental/fiscal principal: cuando existe, se
/// re-parsea (en memoria, nunca persistido) y sus datos fiscales/documentales se fusionan con el
/// Item Matching persistido — ver <see cref="ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraftLineMerger"/>.
/// Esto cubre recepciones antiguas cuyo detalle (incl. <c>purchase_reception_line_taxes</c>) se
/// persistió antes de que el sistema capturara el snapshot fiel de impuestos. Si el re-parseo falla,
/// cae de forma transparente a las líneas persistidas tal cual (nunca bloquea "Crear compra" por
/// esto). Aparte de ese re-parseo de solo lectura, el único reintento que existe es el interno para
/// <see cref="PurchaseReceptionProcessingStatus.Failed"/> (ver más abajo), que sí persiste y jamás
/// se expone como una acción separada.
/// </summary>
public sealed class CreatePurchaseReceptionDraftHandler
    : IRequestHandler<CreatePurchaseReceptionDraftCommand, Result<PurchaseDraftDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IPurchaseInvoiceRepository _purchaseRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IPurchaseReceptionDetailProcessor _detailProcessor;
    private readonly IItemRepository _itemRepo;
    private readonly IPurchaseXmlDraftParser _xmlParser;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ILogger<CreatePurchaseReceptionDraftHandler> _logger;

    public CreatePurchaseReceptionDraftHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        IPurchaseInvoiceRepository purchaseRepo,
        IBusinessPartnerRepository bpRepo,
        IPurchaseReceptionDetailProcessor detailProcessor,
        IItemRepository itemRepo,
        IPurchaseXmlDraftParser xmlParser,
        ICurrentTenant tenant,
        ICurrentUser user,
        ILogger<CreatePurchaseReceptionDraftHandler> logger
    )
    {
        _documentRepo = documentRepo;
        _purchaseRepo = purchaseRepo;
        _bpRepo = bpRepo;
        _detailProcessor = detailProcessor;
        _itemRepo = itemRepo;
        _xmlParser = xmlParser;
        _tenant = tenant;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<PurchaseDraftDto>> Handle(
        CreatePurchaseReceptionDraftCommand request,
        CancellationToken cancellationToken
    )
    {
        var document = await _documentRepo.GetByIdAsync(
            _tenant.TenantId,
            request.PurchaseReceptionDocumentId,
            cancellationToken
        );
        if (document is null)
            return Result<PurchaseDraftDto>.NotFound("El documento de recepción no existe.");

        if (
            document.Status != PurchaseReceptionDocumentStatus.Verified
            || string.IsNullOrWhiteSpace(document.XmlContent)
        )
        {
            return Result<PurchaseDraftDto>.ValidationFailure(
                "Solo se puede generar un borrador de compra desde documentos con XML autorizado (estado Verificado)."
            );
        }

        var existingPurchase = await _purchaseRepo.GetByAccessKeyAsync(
            _tenant.TenantId,
            document.AccessKey,
            cancellationToken
        );
        if (existingPurchase is not null)
            return Result<PurchaseDraftDto>.Conflict(
                "Ya existe una compra registrada con esta clave de acceso SRI."
            );

        if (document.SupplierId is { } documentSupplierId)
        {
            var supplier = await _bpRepo.GetByIdAsync(documentSupplierId, cancellationToken);
            if (supplier is not null && !supplier.IsActive)
                return Result<PurchaseDraftDto>.ValidationFailure(
                    $"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo."
                );
        }

        // Caso recuperable: el detalle nunca se interpretó con éxito (p. ej. porque el parser
        // vigente al momento de la descarga original era menos tolerante que el actual). Se
        // reconstruye el snapshot de forma transparente sobre el XML YA GUARDADO — nunca se vuelve
        // a consultar el SRI — antes de decidir si hay información utilizable para el borrador. El
        // usuario nunca ve esto como un paso separado: es interno a "Crear Compra".
        if (document.ProcessingStatus == PurchaseReceptionProcessingStatus.Failed)
        {
            var reprocessed = await _detailProcessor.ProcessAsync(
                document.Id,
                _tenant.TenantId,
                document.SupplierId,
                document.XmlContent,
                cancellationToken
            );
            document.ReprocessDetail(
                reprocessed.Lines,
                reprocessed.Processing,
                reprocessed.DocTypeCode,
                reprocessed.SriPaymentMethodCode,
                _user.UserId
            );
            await _documentRepo.SaveChangesAsync(cancellationToken);
        }

        // Caso no recuperable: el comprobante es fiscalmente válido (Verified, XML conservado como
        // evidencia) pero, incluso después del intento de reconstrucción anterior, su detalle sigue
        // sin poder interpretarse — no existe información utilizable para armar un borrador. Nunca
        // se genera un draft vacío disfrazado de éxito ni se abre el formulario de Compras sin datos.
        if (document.ProcessingStatus == PurchaseReceptionProcessingStatus.Failed)
        {
            return Result<PurchaseDraftDto>.ValidationFailure(
                "No se pudo interpretar el detalle de este comprobante "
                    + $"({document.ProcessingNotes ?? "motivo no especificado"}). "
                    + "El XML autorizado quedó conservado como evidencia fiscal; contacte soporte para revisión manual."
            );
        }

        var mergedLines = BuildMergedLines(document);
        var draft = DraftModel.FromMergedLines(document, mergedLines);
        var packagingByLine = await ResolvePackagingSnapshotsAsync(document, cancellationToken);

        return Result<PurchaseDraftDto>.Success(DraftMapper.ToDto(draft, packagingByLine));
    }

    /// <summary>
    /// El XML guardado (<see cref="PurchaseReceptionDocument.XmlContent"/>) es la fuente
    /// documental/fiscal principal — <c>document.Lines</c> es un cache/proyección, no la única
    /// fuente de verdad. Si hay XML, se re-parsea (solo lectura, nunca persistido) y se fusiona con
    /// las líneas persistidas para no perder Item Matching. Si no hay XML, o si el re-parseo falla,
    /// se usan las líneas persistidas tal cual — comportamiento idéntico al anterior.
    /// </summary>
    private IReadOnlyList<PurchaseDraftMergedLine> BuildMergedLines(PurchaseReceptionDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.XmlContent))
            return document.Lines.Select(PurchaseDraftMergedLine.FromPersisted).ToList();

        Result<ParsedPurchaseXml> parseResult;
        try
        {
            parseResult = _xmlParser.Parse(document.XmlContent);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // IPurchaseXmlDraftParser.Parse ya envuelve FormatException/ArgumentException/
            // InvalidOperationException en un Result fallido, pero un XmlContent que ni siquiera es
            // XML bien formado (p. ej. un string arbitrario) puede escapar como XmlException sin
            // pasar por ese try/catch interno. No se toca la lógica de parseo (Infraestructura
            // CLOSED) — se blinda aquí, con el mismo resultado: cae al fallback sin fusión.
            _logger.LogWarning(
                ex,
                "El XML del documento de recepción {DocumentId} no se pudo re-interpretar al crear el borrador de compra; se usan las líneas persistidas tal cual.",
                document.Id
            );
            return document.Lines.Select(PurchaseDraftMergedLine.FromPersisted).ToList();
        }

        if (!parseResult.IsSuccess)
        {
            _logger.LogWarning(
                "No se pudo re-interpretar el XML del documento de recepción {DocumentId} al crear el borrador de compra; se usan las líneas persistidas tal cual. Motivo: {Reason}",
                document.Id,
                parseResult.Error
            );
            return document.Lines.Select(PurchaseDraftMergedLine.FromPersisted).ToList();
        }

        return PurchaseDraftLineMerger.Merge(
            document.Lines,
            parseResult.Value!.Lines,
            onAmbiguousGroup: key =>
                _logger.LogWarning(
                    "El documento de recepción {DocumentId} tiene una cantidad distinta de líneas persistidas y líneas frescas del XML para la clave '{Key}' — esas líneas no se fusionan, se mantienen tal cual estaban persistidas.",
                    document.Id,
                    key
                )
        );
    }

    private async Task<IReadOnlyDictionary<Guid, PurchaseDraftLinePackagingSnapshot>> ResolvePackagingSnapshotsAsync(
        PurchaseReceptionDocument document,
        CancellationToken cancellationToken
    )
    {
        var snapshots = new Dictionary<Guid, PurchaseDraftLinePackagingSnapshot>();

        if (document.SupplierId is not { } supplierId)
            return snapshots;

        foreach (var line in document.Lines)
        {
            if (line.ItemId is not { } itemId || string.IsNullOrWhiteSpace(line.SupplierCode))
                continue;

            var match = await _itemRepo.GetSupplierCodeMatchAsync(
                supplierId,
                line.SupplierCode,
                document.TenantId,
                cancellationToken
            );
            if (match is null || match.ItemId != itemId)
                continue;

            if (
                match.PackagingLevelId is { } packagingLevelId
                && !string.IsNullOrWhiteSpace(match.PackagingUomCode)
                && match.PackagingBaseQuantity is > 0m
            )
            {
                snapshots[line.Id] = new PurchaseDraftLinePackagingSnapshot(
                    packagingLevelId,
                    match.PackagingUomCode,
                    match.BaseUomCode,
                    match.PackagingBaseQuantity.Value
                );
                continue;
            }

            snapshots[line.Id] = PurchaseDraftLinePackagingSnapshot.Base(match.BaseUomCode);
        }

        return snapshots;
    }
}

using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;
using DraftMapper = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraftMapper;
using DraftModel = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraft;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;

/// <summary>
/// Arma el borrador de compra exclusivamente desde el <see cref="ERP.Domain.Modules.Purchases.PurchaseReception.Entities.PurchaseReceptionDocument"/>
/// ya verificado — nunca vuelve a parsear el XML del lado del usuario. Cabecera SRI y líneas (incl.
/// Item Matching ya resuelto: <c>ItemId</c>/<c>MatchStatus</c>) fueron persistidas una única vez al
/// descargar el XML (<c>DownloadPurchaseReceptionXmlHandler</c>). Single Source of Truth: el XML
/// queda solo para auditoría/evidencia, nunca como mecanismo de reconstrucción del borrador desde
/// la experiencia del usuario — el único reintento que existe es interno (ver más abajo) y jamás se
/// expone como una acción separada.
/// </summary>
public sealed class CreatePurchaseReceptionDraftHandler
    : IRequestHandler<CreatePurchaseReceptionDraftCommand, Result<PurchaseDraftDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IPurchaseReceptionDetailProcessor _detailProcessor;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreatePurchaseReceptionDraftHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        IPurchaseReceptionDetailProcessor detailProcessor,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _documentRepo = documentRepo;
        _detailProcessor = detailProcessor;
        _tenant = tenant;
        _user = user;
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

        var draft = DraftModel.FromReceptionDocument(document);

        return Result<PurchaseDraftDto>.Success(DraftMapper.ToDto(draft));
    }
}

using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Services;

/// <summary>
/// FASE 5 PURCHASE-UX — único armado de una fila de Recepción (<see cref="PurchaseReceptionItemDto"/>)
/// a partir del resultado del verificador + el documento guardado. Lo comparten la importación del TXT
/// y el refresco de estados, así ambos devuelven exactamente el mismo contrato. Solo lectura.
/// </summary>
public static class PurchaseReceptionItemAssembler
{
    public static async Task<PurchaseReceptionItemDto> BuildAsync(
        IPurchaseCreditNoteRepository creditNotes,
        Guid tenantId,
        PurchaseReceptionVerifiedItem item,
        PurchaseReceptionDocument document,
        DateTime authorizationDateUtc,
        CancellationToken ct
    )
    {
        // PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — solo aplica a notas de crédito;
        // GetIdByReceptionDocumentIdAsync ya excluye Cancelled (a lo sumo una NC ACTIVA por
        // receptionDocumentId, ver PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01).
        Guid? creditNoteId = null;
        Guid? cancelledCreditNoteId = null;
        if (item.Record.SourceDocType == PurchaseReceptionSourceDocType.CreditNote)
        {
            creditNoteId = await creditNotes.GetIdByReceptionDocumentIdAsync(tenantId, document.Id, ct);
            // Historial ("Ver NC anulada") — solo tiene sentido consultarlo cuando no hay una
            // activa bloqueando el reintento; con una activa, la UI ya muestra "NC ya procesada".
            if (creditNoteId is null)
                cancelledCreditNoteId = await creditNotes.GetLatestCancelledIdByReceptionDocumentIdAsync(
                    tenantId,
                    document.Id,
                    ct
                );
        }

        return PurchaseReceptionMapper.ToDto(
            item,
            document,
            authorizationDateUtc,
            creditNoteExists: creditNoteId is not null,
            creditNoteId: creditNoteId,
            cancelledCreditNoteId: cancelledCreditNoteId
        );
    }
}

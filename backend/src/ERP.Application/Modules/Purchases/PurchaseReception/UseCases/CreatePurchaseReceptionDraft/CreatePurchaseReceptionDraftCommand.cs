using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;

/// <summary>
/// Parsea el XML ya almacenado de un <c>PurchaseReceptionDocument</c> (Fase 3) y arma un
/// <see cref="Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraft"/> para
/// precargar el formulario de Nueva Compra. Nunca crea ni persiste una <c>PurchaseInvoice</c> — el
/// guardado real sigue ocurriendo exclusivamente vía <c>CreatePurchaseDraftCommand</c> desde el
/// formulario, con los datos ya cargados y editables por el usuario.
/// </summary>
public sealed record CreatePurchaseReceptionDraftCommand(Guid PurchaseReceptionDocumentId)
    : IRequest<Result<PurchaseDraftDto>>, IBranchScopedRequest;

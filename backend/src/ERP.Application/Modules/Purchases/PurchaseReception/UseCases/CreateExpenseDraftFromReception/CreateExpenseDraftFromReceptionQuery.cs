using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreateExpenseDraftFromReception;

/// <summary>
/// EXPENSES-FROM-RECEPTION-01 — arma la cabecera de un borrador de Gasto desde un
/// <c>PurchaseReceptionDocument</c> ya verificado (factura, nunca nota de crédito). Nunca crea ni
/// persiste un <c>ExpenseDocument</c> — el guardado real sigue ocurriendo exclusivamente vía
/// <c>CreateExpenseDraftCommand</c> desde el formulario de Gastos, con
/// <see cref="ExpenseReceptionDraftDto.ReceptionDocumentId"/>/<see cref="ExpenseReceptionDraftDto.AccessKey"/>
/// ya cargados para que el backend valide el bloqueo cruzado Compra↔Gasto al guardar.
/// </summary>
public sealed record CreateExpenseDraftFromReceptionQuery(Guid PurchaseReceptionDocumentId)
    : IRequest<Result<ExpenseReceptionDraftDto>>,
        IBranchScopedRequest;

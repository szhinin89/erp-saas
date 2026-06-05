using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.SupplierNotes;

public sealed record GetPurchaseSupplierNotesQuery(
    Guid? BusinessPartnerId,
    Guid?   PurchBillId,
    Guid?   ExpenseInvoiceId,
    string?   Status
) : IRequest<Result<IReadOnlyList<SupplierPurchaseNoteDto>>>, ICompanyScopedRequest;

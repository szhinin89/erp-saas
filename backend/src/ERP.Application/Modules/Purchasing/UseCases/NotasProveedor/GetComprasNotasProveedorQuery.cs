using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed record GetPurchasesNotesSupplierQuery(
    Guid? SupplierId,
    Guid?   PurchBillId,
    Guid?   ExpenseInvoiceId,
    string?   Status
) : IRequest<Result<IReadOnlyList<SupplierPurchaseNoteDto>>>;

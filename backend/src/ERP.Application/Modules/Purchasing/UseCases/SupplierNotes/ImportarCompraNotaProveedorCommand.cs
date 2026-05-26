using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.SupplierNotes;

public sealed record ImportPurchaseSupplierNoteCommand(
    byte[]  XmlContent,
    string? XmlFileName,
    Guid?   PurchBillId,
    Guid?   ExpenseInvoiceId
) : IRequest<Result<SupplierPurchaseNoteDto>>, ICompanyScopedRequest;

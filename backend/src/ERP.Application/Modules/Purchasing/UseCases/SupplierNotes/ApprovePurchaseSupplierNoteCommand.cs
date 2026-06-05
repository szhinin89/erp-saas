using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.SupplierNotes;

public sealed record ApprovePurchaseSupplierNoteCommand(
    Guid    NoteId,
    string?   AuthNumber,
    DateTime? AuthDate
) : IRequest<Result<SupplierPurchaseNoteDto>>, ICompanyScopedRequest;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed record ApprovePurchaseSupplierNoteCommand(
    Guid    NotaId,
    string?   AuthNumber,
    DateTime? AuthDate
) : IRequest<Result<SupplierPurchaseNoteDto>>;

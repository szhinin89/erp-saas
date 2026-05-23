using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateSupplierCommand(
    string  PersonType,
    string  LegalName,
    string  Ruc,
    string? Email,
    string? Phone,
    string? Address,
    string  PaymentTerms
) : IRequest<Result<SupplierDto>>, ICompanyScopedRequest;

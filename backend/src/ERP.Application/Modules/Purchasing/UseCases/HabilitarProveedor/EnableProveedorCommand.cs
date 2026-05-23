using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.HabilitarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableSupplierCommand(Guid Id)
    : IRequest<Result<SupplierDto>>, ICompanyScopedRequest;

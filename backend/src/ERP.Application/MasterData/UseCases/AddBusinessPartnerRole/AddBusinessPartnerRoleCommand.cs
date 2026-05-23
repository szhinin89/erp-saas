using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AddBusinessPartnerRole;

/// <summary>
/// Agrega un rol (Customer y/o Supplier) a un BusinessPartner existente.
/// Idempotente: ignora perfiles que ya existen.
/// </summary>
public sealed record AddBusinessPartnerRoleCommand(
    Guid Id,
    bool AsCustomer,
    bool AsSupplier) : IRequest<Result<bool>>, ISubscriberOnlyRequest;

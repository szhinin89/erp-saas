using MediatR;
using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;

namespace ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;

public record UpdateTenantGlobalParametersCommand(
    Guid TenantId,
    bool ElectronicBillingTrialEnabled) : IRequest<Result<TenantDto>>;

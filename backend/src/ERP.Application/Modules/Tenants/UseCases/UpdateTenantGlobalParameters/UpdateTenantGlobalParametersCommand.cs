namespace ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;

public record UpdateTenantGlobalParametersCommand(
    Guid TenantId,
    bool ElectronicBillingTrialEnabled);


using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using ERP.Domain.Access.Entities;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Common;

public sealed record SubscriberProvisioningRequest(
    string SubscriberName,
    string SubscriberSlug,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string? AdminPassword,
    Guid ActorId,
    PasswordResetMode PasswordResetMode = PasswordResetMode.Disabled,
    int DisplayOrder = 0,
    int Priority = 0,
    string? PlanCode = null,
    IReadOnlyList<string>? EnabledModules = null,
    bool LinkExistingAdmin = false,
    string? CountryCode = "ECU",
    string? Timezone = "America/Guayaquil",
    string? AuditDescription = null);

public sealed record SubscriberProvisioningResult(
    Subscriber Subscriber,
    IdentityUser AdminUser,
    CompanyEntity DefaultCompany,
    CompanyUserMembership Membership);

public interface ISubscriberProvisioningOrchestrator
{
    Task<SubscriberProvisioningResult> ProvisionNewSubscriberWithAdminAsync(
        SubscriberProvisioningRequest request,
        CancellationToken ct = default);

    Task<SubscriberProvisioningResult> ProvisionSubscriberWithExistingAdminAsync(
        SubscriberProvisioningRequest request,
        IdentityUser existingAdmin,
        CancellationToken ct = default);
}

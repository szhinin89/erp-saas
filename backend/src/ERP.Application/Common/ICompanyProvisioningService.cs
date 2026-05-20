using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Common;

/// <summary>
/// Crea o sincroniza la empresa fiscal (RUC) desde el perfil del suscriptor.
/// </summary>
public interface ICompanyProvisioningService
{
    Task<CompanyEntity> EnsureDefaultCompanyAsync(Subscriber subscriber, CancellationToken ct = default);

    /// <summary>
    /// Crea una empresa fiscal bajo el suscriptor, con enforcement de <c>MAX_COMPANIES</c>
    /// y membresía activa para el usuario creador.
    /// </summary>
    Task<CompanyEntity> CreateManagedCompanyAsync(
        Guid subscriberId,
        string ruc,
        string legalName,
        string mainAddress,
        Guid createdByUserId,
        string creatorRole,
        string? tradeName = null,
        string? email = null,
        string? phone = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? logoUrl = null,
        string? brandingJson = null,
        CancellationToken ct = default);
}

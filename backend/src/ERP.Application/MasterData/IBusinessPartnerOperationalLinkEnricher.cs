using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData;

/// <summary>
/// Enriquece DTOs MasterData con vínculos operacionales legacy (Customer/Supplier) y perfiles.
/// </summary>
public interface IBusinessPartnerOperationalLinkEnricher
{
    Task<BusinessPartnerDto> EnrichAsync(BusinessPartner partner, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessPartnerDto>> EnrichAsync(
        IReadOnlyList<BusinessPartner> partners,
        CancellationToken ct = default);
}

using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record CompanyBpPurchaseSettingsDto(
    Guid Id,
    Guid BusinessPartnerId,
    Guid? PaymentTermId,
    bool HasCustomConfiguration
)
{
    public static CompanyBpPurchaseSettingsDto From(CompanyBpPurchaseSettings s) =>
        new(s.Id, s.BusinessPartnerId, s.PaymentTermId, HasCustomConfiguration: true);

    public static CompanyBpPurchaseSettingsDto Defaults(Guid businessPartnerId) =>
        new(
            Id: Guid.Empty,
            BusinessPartnerId: businessPartnerId,
            PaymentTermId: null,
            HasCustomConfiguration: false
        );
}

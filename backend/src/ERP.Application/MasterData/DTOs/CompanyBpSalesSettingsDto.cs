using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record CompanyBpSalesSettingsDto(
    Guid Id,
    Guid BusinessPartnerId,
    Guid? PaymentTermId,
    bool HasCustomConfiguration
)
{
    public static CompanyBpSalesSettingsDto From(CompanyBpSalesSettings s) =>
        new(s.Id, s.BusinessPartnerId, s.PaymentTermId, HasCustomConfiguration: true);

    public static CompanyBpSalesSettingsDto Defaults(Guid businessPartnerId) =>
        new(
            Id: Guid.Empty,
            BusinessPartnerId: businessPartnerId,
            PaymentTermId: null,
            HasCustomConfiguration: false
        );
}

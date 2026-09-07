using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpPurchaseSettings;

public sealed class UpsertCompanyBpPurchaseSettingsValidator
    : AbstractValidator<UpsertCompanyBpPurchaseSettingsCommand>
{
    public UpsertCompanyBpPurchaseSettingsValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
    }
}

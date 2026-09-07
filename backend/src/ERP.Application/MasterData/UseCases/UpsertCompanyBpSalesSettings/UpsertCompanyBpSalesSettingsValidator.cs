using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;

public sealed class UpsertCompanyBpSalesSettingsValidator
    : AbstractValidator<UpsertCompanyBpSalesSettingsCommand>
{
    public UpsertCompanyBpSalesSettingsValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
    }
}

using ERP.Domain.MasterData.Entities;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.BlockBusinessPartner;

public sealed class BlockBusinessPartnerValidator : AbstractValidator<BlockBusinessPartnerCommand>
{
    public BlockBusinessPartnerValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de bloqueo es obligatorio.")
            .MaximumLength(CompanyBpTradingSettings.BlockedReasonMaxLen)
            .WithMessage($"El motivo no puede superar {CompanyBpTradingSettings.BlockedReasonMaxLen} caracteres.");
    }
}

using FluentValidation;

namespace ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;

public sealed class AssignBusinessPartnerRoleValidator : AbstractValidator<AssignBusinessPartnerRoleCommand>
{
    public AssignBusinessPartnerRoleValidator()
    {
        RuleFor(x => x.BusinessPartnerId)
            .NotEmpty().WithMessage("BusinessPartnerId es obligatorio.");

        RuleFor(x => x.RoleType)
            .IsInEnum().WithMessage("Tipo de rol inválido.");
    }
}

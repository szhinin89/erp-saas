using FluentValidation;

namespace ERP.Application.Auth.UseCases.OperateCompany;

public sealed class OperateCompanyCommandValidator : AbstractValidator<OperateCompanyCommand>
{
    public OperateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
    }
}

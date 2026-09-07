using FluentValidation;

namespace ERP.Application.MasterData.UseCases.SetSupplierRetentionDefaultState;

public sealed class SetSupplierRetentionDefaultStateValidator
    : AbstractValidator<SetSupplierRetentionDefaultStateCommand>
{
    public SetSupplierRetentionDefaultStateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

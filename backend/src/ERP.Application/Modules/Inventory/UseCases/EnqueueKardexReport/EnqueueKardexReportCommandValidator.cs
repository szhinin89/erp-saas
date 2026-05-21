using FluentValidation;

namespace ERP.Application.Inventory.UseCases.EnqueueKardexReport;

public sealed class EnqueueKardexReportCommandValidator : AbstractValidator<EnqueueKardexReportCommand>
{
    public EnqueueKardexReportCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        When(x => x.StartDate.HasValue && x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate)
                .Must((cmd, end) => end!.Value.Date >= cmd.StartDate!.Value.Date)
                .WithMessage("fechaFin debe ser posterior o igual a fechaInicio.");
        });
    }
}

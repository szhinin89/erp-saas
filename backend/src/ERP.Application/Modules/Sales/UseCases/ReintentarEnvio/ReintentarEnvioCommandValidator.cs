using FluentValidation;

namespace ERP.Application.Sales.UseCases.ReintentarEnvio;

public sealed class ReintentarEnvioCommandValidator : AbstractValidator<RetrySubmissionCommand>
{
    public ReintentarEnvioCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}

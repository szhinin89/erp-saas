using FluentValidation;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.InspectSriCertificate;

public sealed class InspectSriCertificateQueryValidator
    : AbstractValidator<InspectSriCertificateQuery>
{
    public InspectSriCertificateQueryValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Debe ingresar la contraseña del certificado.");
    }
}

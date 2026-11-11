using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.TaxId)
            .NotEmpty()
            .WithMessage("El RUC / identificador fiscal es obligatorio.")
            .Length(13)
            .WithMessage("El RUC debe tener 13 caracteres.");

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));

        When(
            x => x.InitialAdministrator is not null,
            () =>
            {
                // Mismas reglas de identidad que CreateSystemUserCommandValidator (sin contraseña:
                // la genera el backend).
                RuleFor(x => x.InitialAdministrator!.Username)
                    .NotEmpty()
                    .Matches("^[a-zA-Z0-9][a-zA-Z0-9._-]{1,48}[a-zA-Z0-9]$")
                    .WithMessage(
                        "El username debe tener 3-50 caracteres: letras, números, punto, guion o guion bajo."
                    );
                RuleFor(x => x.InitialAdministrator!.FirstName)
                    .NotEmpty()
                    .WithMessage("Los nombres del administrador son obligatorios.")
                    .MaximumLength(50);
                RuleFor(x => x.InitialAdministrator!.LastName)
                    .NotEmpty()
                    .WithMessage("Los apellidos del administrador son obligatorios.")
                    .MaximumLength(50);
                RuleFor(x => x.InitialAdministrator!.Email)
                    .NotEmpty()
                    .WithMessage("El correo del administrador es obligatorio.")
                    .EmailAddress()
                    .MaximumLength(256);
            }
        );
    }
}

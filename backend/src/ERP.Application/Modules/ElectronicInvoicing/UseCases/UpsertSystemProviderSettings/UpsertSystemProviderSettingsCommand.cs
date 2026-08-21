using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings;

/// <summary>No es company-scoped — el proveedor de sistema es único por instancia del ERP.</summary>
public sealed record UpsertSystemProviderSettingsCommand(
    string? Ruc,
    string? LegalName,
    string? CiiuCode,
    DateOnly? EffectiveDate,
    bool Enabled
) : IRequest<Result<SystemProviderSettingsDto>>;

public sealed class UpsertSystemProviderSettingsCommandValidator
    : AbstractValidator<UpsertSystemProviderSettingsCommand>
{
    public UpsertSystemProviderSettingsCommandValidator()
    {
        RuleFor(x => x.Ruc)
            .Length(SystemProviderSettings.RucLength)
            .Matches("^[0-9]+$")
            .WithMessage($"El RUC debe tener {SystemProviderSettings.RucLength} dígitos numéricos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ruc));
        RuleFor(x => x.LegalName)
            .MaximumLength(SystemProviderSettings.LegalNameMaxLen)
            .When(x => x.LegalName is not null);
        RuleFor(x => x.CiiuCode)
            .MaximumLength(SystemProviderSettings.CiiuCodeMaxLen)
            .When(x => x.CiiuCode is not null);
        RuleFor(x => x)
            .Must(x =>
                !x.Enabled
                || (
                    !string.IsNullOrWhiteSpace(x.Ruc)
                    && !string.IsNullOrWhiteSpace(x.LegalName)
                    && !string.IsNullOrWhiteSpace(x.CiiuCode)
                )
            )
            .WithMessage(
                "No se puede habilitar el proveedor de sistema sin RUC, razón social y CIIU completos."
            );
    }
}

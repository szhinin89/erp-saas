using System.Text.RegularExpressions;
using ERP.Domain.Common.Validators;
using FluentValidation;

namespace ERP.Application.Modules.Integration.UseCases;

/// <summary>
/// API-DOC-V1 F-09: el RUC de <c>POST /api/integration/v1/companies</c> debe ser un RUC ecuatoriano
/// válido (<see cref="RucValidator"/>) o un identificador provisional <c>TMP-EC-</c> con el formato
/// que genera el sistema, sin espacios iniciales ni finales. Se rechaza antes de aprovisionar: un valor
/// inválido nunca se rellena con ceros, se trunca ni se recorta. También rechaza un <c>brandingJson</c> no
/// vacío (F-01): el campo está reservado y no se persiste.
/// </summary>
public sealed partial class CreateIntegrationCompanyCommandValidator
    : AbstractValidator<CreateIntegrationCompanyCommand>
{
    public const string UnsupportedBrandingMessage =
        "El campo brandingJson no está soportado en v1: omítalo o envíelo vacío.";

    public const string InvalidTaxIdMessage =
        "El RUC debe ser un RUC ecuatoriano válido de 13 dígitos o un identificador provisional TMP-EC.";

    // Formato de ProvisionalTaxIdGenerator.Create(): "TMP-EC-" + 8 caracteres hexadecimales.
    [GeneratedRegex("^TMP-EC-[0-9A-Fa-f]{8}$", RegexOptions.IgnoreCase)]
    private static partial Regex ProvisionalFormat();

    public CreateIntegrationCompanyCommandValidator()
    {
        RuleFor(x => x.Request.TaxId)
            .Must(IsAcceptedTaxId)
            .WithMessage(InvalidTaxIdMessage);

        // API-DOC-V1 F-01: brandingJson se conserva en el contrato v1 pero no se admite su uso hasta
        // definir su contrato; nunca se persiste (antes llegaba por error a Company.Website).
        RuleFor(x => x.Request.BrandingJson)
            .Must(string.IsNullOrWhiteSpace)
            .WithMessage(UnsupportedBrandingMessage);
    }

    private static bool IsAcceptedTaxId(string? taxId)
    {
        // Sin espacios iniciales ni finales: lo validado es exactamente lo que se persiste.
        if (string.IsNullOrWhiteSpace(taxId) || taxId != taxId.Trim())
            return false;
        return ProvisionalFormat().IsMatch(taxId) || RucValidator.EsRucValido(taxId);
    }
}

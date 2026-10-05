using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6, diseño §I.3) — única composición de
/// <c>infoAdicional</c>:
/// <list type="number">
/// <item>Contributors que aplican al tipo, por <c>Order</c> ascendente (empate → <c>Id</c> ordinal);
/// luego los <c>sourceFields</c> en el orden del provider.</item>
/// <item>Nombres únicos comparados con <c>Trim</c> y sin distinguir mayúsculas. Los nombres que
/// producen los contributors son reservados: un campo del documento no puede suplantarlos.</item>
/// <item>Nombre y valor de 1 a 300 caracteres (XSD <c>nombre</c>/<c>campoAdicional</c>); máximo 15
/// campos en total. Nunca se trunca ni se reescribe un valor: se emite tal cual o se falla.</item>
/// <item>Un contributor que falla corta la composición: no hay XML (fail-closed).</item>
/// </list>
/// Sin reloj ni aleatoriedad: el mismo contexto produce el mismo resultado (vista previa, pipeline y
/// regeneración coinciden). Si ningún contributor aporta campos, devuelve la misma instancia de
/// <c>sourceFields</c>, de modo que el modelo fiscal queda idéntico al del provider.
/// </summary>
public sealed partial class ElectronicDocumentAdditionalInfoComposer
    : IElectronicDocumentAdditionalInfoComposer
{
    /// <summary>Ficha Técnica SRI / XSD: máximo de nodos <c>campoAdicional</c> por comprobante.</summary>
    public const int MaxFields = 15;

    /// <summary>XSD tipos <c>nombre</c> y <c>campoAdicional</c>: <c>minLength 1</c>, <c>maxLength 300</c>.</summary>
    public const int MaxLength = 300;

    private readonly IReadOnlyList<IElectronicDocumentAdditionalInfoContributor> _contributors;
    private readonly ILogger<ElectronicDocumentAdditionalInfoComposer> _logger;

    public ElectronicDocumentAdditionalInfoComposer(
        IEnumerable<IElectronicDocumentAdditionalInfoContributor> contributors,
        ILogger<ElectronicDocumentAdditionalInfoComposer> logger
    )
    {
        _contributors = contributors
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ComposeAsync(
        AdditionalInfoCompositionContext context,
        IReadOnlyList<ElectronicDocumentAdditionalField> sourceFields,
        CancellationToken ct = default
    )
    {
        var composed = new List<ElectronicDocumentAdditionalField>();
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var contributor in _contributors)
        {
            if (!contributor.AppliesTo(context.DocumentType))
                continue;

            var contribution = await contributor.ContributeAsync(context, ct);
            if (!contribution.IsSuccess)
                return Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.ValidationFailure(
                    contribution.Error
                        ?? $"No se pudo componer la información adicional ({contributor.Id}).",
                    contribution.Code ?? ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid
                );

            foreach (var field in contribution.Value ?? [])
            {
                var error = ValidateField(field, $"campo normativo ({contributor.Id})");
                if (error is not null)
                    return Invalid(error);
                if (!reservedNames.Add(field.Name.Trim()))
                    return Invalid(
                        $"El campo adicional '{field.Name.Trim()}' está duplicado entre las reglas normativas."
                    );

                composed.Add(field);
                LogNormativeFieldComposed(
                    contributor.Id,
                    field.Name,
                    context.DocumentType.ToString()
                );
            }
        }

        var usedNames = new HashSet<string>(reservedNames, StringComparer.OrdinalIgnoreCase);
        foreach (var field in sourceFields)
        {
            var error = ValidateField(field, "campo del documento");
            if (error is not null)
                return Invalid(error);

            var name = field.Name.Trim();
            if (reservedNames.Contains(name))
                return Invalid(
                    $"El campo adicional '{name}' está reservado por una regla normativa del SRI y no puede usarse en el documento."
                );
            if (!usedNames.Add(name))
                return Invalid($"El campo adicional '{name}' está duplicado.");

            composed.Add(field);
        }

        if (composed.Count > MaxFields)
            return Invalid(
                $"El comprobante tiene {composed.Count} campos adicionales; el SRI permite un máximo de {MaxFields}."
            );

        return Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(
            reservedNames.Count == 0 ? sourceFields : composed
        );
    }

    private static string? ValidateField(ElectronicDocumentAdditionalField field, string origin)
    {
        if (string.IsNullOrWhiteSpace(field.Name))
            return $"Un {origin} de la información adicional no tiene nombre.";
        if (field.Name.Length > MaxLength)
            return $"El nombre del campo adicional '{Preview(field.Name)}' supera {MaxLength} caracteres.";
        if (string.IsNullOrWhiteSpace(field.Value))
            return $"El campo adicional '{field.Name.Trim()}' no tiene valor.";
        if (field.Value.Length > MaxLength)
            return $"El valor del campo adicional '{field.Name.Trim()}' supera {MaxLength} caracteres; el SRI no admite truncarlo.";
        return null;
    }

    private static string Preview(string value) =>
        value.Length <= 40 ? value.Trim() : value[..40].Trim() + "…";

    private static Result<IReadOnlyList<ElectronicDocumentAdditionalField>> Invalid(string error) =>
        Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.ValidationFailure(
            error,
            ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid
        );

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[ElectronicDocuments] infoAdicional: campo normativo '{FieldName}' aportado por {ContributorId} ({DocumentType})"
    )]
    private partial void LogNormativeFieldComposed(
        string contributorId,
        string fieldName,
        string documentType
    );
}

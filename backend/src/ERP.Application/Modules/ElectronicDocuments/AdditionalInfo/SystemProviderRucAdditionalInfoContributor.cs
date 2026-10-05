using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;

namespace ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7) — emite
/// <c>&lt;campoAdicional nombre="RUC Proveedor"&gt;RUC&lt;/campoAdicional&gt;</c> (Ficha Técnica 2.34,
/// Anexo 26; Res. NAC-DGERCGC26-00000027) desde <see cref="SystemProviderSettings"/>, configuración
/// GLOBAL de la instancia (no por tenant/empresa: el contexto trae tenant/empresa, pero no se usan para
/// leerla). Es el único consumidor fiscal de <see cref="ISystemProviderSettingsRepository"/>.
///
/// <c>EffectiveDate</c> es la fecha de aplicabilidad fiscal y se compara SOLO con la fecha de emisión
/// de negocio del comprobante (nunca con el reloj):
/// <list type="table">
/// <item>sin fecha + deshabilitado (o sin configuración) → sin campo (requisito no activado);</item>
/// <item>sin fecha + habilitado → fallo (configuración incompleta);</item>
/// <item>emisión anterior a la fecha → sin campo, sin evaluar <c>Enabled</c> ni el RUC;</item>
/// <item>emisión desde la fecha → campo si está habilitado y el RUC es válido; si no, fallo.</item>
/// </list>
/// Todo fallo usa <see cref="ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured"/>: el
/// XML no se genera y el documento queda <c>Failed</c>, reintentable al corregir la configuración.
/// </summary>
public sealed class SystemProviderRucAdditionalInfoContributor
    : IElectronicDocumentAdditionalInfoContributor
{
    public const string ContributorId = "sri.anexo26.system-provider-ruc";

    /// <summary>Anexo 26 aplica a todos los comprobantes; aquí, a los tipos electrónicos implementados.</summary>
    private static readonly HashSet<ElectronicDocumentType> ApplicableTypes =
    [
        ElectronicDocumentType.Invoice,
        ElectronicDocumentType.CreditNote,
        ElectronicDocumentType.Retention,
    ];

    private static readonly IReadOnlyList<ElectronicDocumentAdditionalField> NoFields = [];

    private readonly ISystemProviderSettingsRepository _settings;

    public SystemProviderRucAdditionalInfoContributor(ISystemProviderSettingsRepository settings) =>
        _settings = settings;

    public string Id => ContributorId;

    public int Order => 100;

    public bool AppliesTo(ElectronicDocumentType documentType) =>
        ApplicableTypes.Contains(documentType);

    public async Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ContributeAsync(
        AdditionalInfoCompositionContext context,
        CancellationToken ct = default
    )
    {
        var settings = await _settings.GetAsync(ct);
        var enabled = settings?.Enabled == true;
        var effectiveDate = settings?.EffectiveDate;

        if (effectiveDate is null)
            return enabled
                ? NotConfigured(
                    "El proveedor tecnológico está habilitado pero no tiene la fecha desde la cual el RUC del proveedor es obligatorio. No se generó el comprobante electrónico."
                )
                : Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(NoFields);

        if (context.IssueDate < effectiveDate.Value)
            return Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(NoFields);

        if (!enabled)
            return NotConfigured(
                $"El RUC del proveedor tecnológico es obligatorio desde {effectiveDate.Value:yyyy-MM-dd} y la configuración global del proveedor está deshabilitada. No se generó el comprobante electrónico."
            );

        if (!SystemProviderSettings.IsValidRuc(settings!.Ruc))
            return NotConfigured(
                $"El RUC del proveedor tecnológico es obligatorio desde {effectiveDate.Value:yyyy-MM-dd} y no hay un RUC válido de {SystemProviderSettings.RucLength} dígitos configurado. No se generó el comprobante electrónico."
            );

        return Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success([
            new ElectronicDocumentAdditionalField(
                SriAdditionalInfoFieldNames.SystemProviderRuc,
                settings.Ruc!
            ),
        ]);
    }

    private static Result<IReadOnlyList<ElectronicDocumentAdditionalField>> NotConfigured(
        string error
    ) =>
        Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.ValidationFailure(
            error,
            ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured
        );
}

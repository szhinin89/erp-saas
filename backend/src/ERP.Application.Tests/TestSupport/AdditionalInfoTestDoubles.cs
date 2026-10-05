using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 — composers reales de <c>infoAdicional</c> para tests de los
/// orquestadores. Se usa siempre la implementación real (no un mock): así los tests de los
/// orquestadores ejercen las mismas reglas que producción.
/// </summary>
public static class AdditionalInfoTestDoubles
{
    /// <summary>Composer sin contributors: devuelve los campos del documento tal cual (comportamiento previo al P0).</summary>
    public static IElectronicDocumentAdditionalInfoComposer PassThroughComposer() =>
        new ElectronicDocumentAdditionalInfoComposer(
            [],
            NullLogger<ElectronicDocumentAdditionalInfoComposer>.Instance
        );

    /// <summary>Composer de producción con el contributor RUC Proveedor sobre la configuración indicada.</summary>
    public static IElectronicDocumentAdditionalInfoComposer ComposerWithProviderRuc(
        Mock<ISystemProviderSettingsRepository> settings
    ) =>
        new ElectronicDocumentAdditionalInfoComposer(
            [new SystemProviderRucAdditionalInfoContributor(settings.Object)],
            NullLogger<ElectronicDocumentAdditionalInfoComposer>.Instance
        );

    public const string ProviderRuc = "1792146739001";

    /// <summary>Repositorio con <see cref="SystemProviderSettings"/> configurado vía el método de dominio real.</summary>
    public static Mock<ISystemProviderSettingsRepository> ProviderSettings(
        bool enabled,
        DateOnly? effectiveDate,
        string? ruc = ProviderRuc
    )
    {
        var repo = new Mock<ISystemProviderSettingsRepository>();
        SystemProviderSettings? settings = null;
        if (enabled || effectiveDate is not null || ruc is not null)
        {
            settings = SystemProviderSettings.CreateNew();
            // Configure exige fecha para habilitar; los estados "habilitado sin fecha" o "RUC
            // inválido" (filas heredadas previas al invariante) se construyen por reflexión.
            settings.Configure(
                ruc,
                "ZH Technologies S.A.",
                "J62021002",
                effectiveDate,
                enabled: false,
                Guid.NewGuid()
            );
            if (enabled)
                SetPrivate(settings, nameof(SystemProviderSettings.Enabled), true);
        }

        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        return repo;
    }

    /// <summary>Simula una fila heredada con un RUC que hoy el dominio rechazaría.</summary>
    public static void ForceRuc(SystemProviderSettings settings, string? ruc) =>
        SetPrivate(settings, nameof(SystemProviderSettings.Ruc), ruc);

    private static void SetPrivate(
        SystemProviderSettings settings,
        string property,
        object? value
    ) => typeof(SystemProviderSettings).GetProperty(property)!.SetValue(settings, value);
}

using ERP.Domain.Configuration.Definitions.Modules;

namespace ERP.Domain.Configuration.Definitions;

/// <summary>
/// CONFIG-FOUNDATION-P1-03: registro estático único de toda ConfigurationDefinition activa.
/// A partir de este bloque, org_settings no acepta keys libres — toda escritura se valida contra
/// este catálogo (ver OrgSettingsRepository.UpsertAsync). Un nuevo setting requiere agregar su
/// ConfigurationDefinition aquí (vía el archivo del módulo correspondiente en
/// Definitions/Modules/) antes de poder escribirse — no hay forma de crear una key "al vuelo".
///
/// Namespaces legacy eliminados en entregas previas (ride.branding.*, decimal.* de
/// GeneralParameter) NO están registrados aquí a propósito: cualquier intento de escribirlos hoy
/// falla como key desconocida, que es exactamente el comportamiento correcto — no existe camino
/// de código que los genere, pero si alguien los reintrodujera por error, el guardrail los
/// bloquea igual que cualquier otra key inventada.
/// </summary>
public static class ConfigurationDefinitionCatalog
{
    public static IReadOnlyDictionary<string, ConfigurationDefinition> All { get; } = Build();

    public static bool TryGet(string key, out ConfigurationDefinition? definition) =>
        All.TryGetValue(key, out definition);

    private static IReadOnlyDictionary<string, ConfigurationDefinition> Build()
    {
        var definitions = InvoiceConfigurationDefinitions
            .All()
            .Concat(SalesConfigurationDefinitions.All())
            .Concat(PresentationConfigurationDefinitions.All())
            .Concat(CompanyBrandingConfigurationDefinitions.All())
            .Concat(CatalogConfigurationDefinitions.All())
            .ToList();

        var duplicates = definitions
            .GroupBy(d => d.Key)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"ConfigurationDefinitionCatalog tiene keys duplicadas: {string.Join(", ", duplicates)}. "
                    + "Cada key debe declararse en un único módulo de Definitions."
            );

        foreach (var definition in definitions)
        {
            if (definition.AllowedScopes.Count == 0)
                throw new InvalidOperationException(
                    $"La definition '{definition.Key}' no declara ningún AllowedScope."
                );

            if (!definition.AllowedScopes.Contains(definition.DefaultScope))
                throw new InvalidOperationException(
                    $"La definition '{definition.Key}' tiene DefaultScope '{definition.DefaultScope}' "
                        + "fuera de su propia lista de AllowedScopes."
                );
        }

        return definitions.ToDictionary(d => d.Key, d => d);
    }
}

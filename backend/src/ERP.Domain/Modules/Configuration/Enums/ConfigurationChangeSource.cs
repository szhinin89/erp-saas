namespace ERP.Domain.Configuration.Enums;

/// <summary>CONFIG-FOUNDATION-P2-01: origen de un cambio registrado en ConfigurationChangeLog.</summary>
public enum ConfigurationChangeSource
{
    AdminUI,
    Api,
    Migration,
    System,
    Seed,
}

using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

/// <summary>Configuración de alcance feature por tenant (máxima prioridad).</summary>
public sealed class ConfigFeature : AuditableEntity
{
    public const int FeatureMaxLength = 120;
    public const int KeyMaxLength = 200;
    public const int DataTypeMaxLength = 32;
    public const int ValueMaxLength = 4000;

    public string Feature { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string DataType { get; private set; } = "string";

    private ConfigFeature() { }

    public static ConfigFeature Create(Guid subscriberId, string feature, string key, string value, string dataType, Guid userId)
    {
        var row = new ConfigFeature
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Feature = NormalizeScope(feature),
            Key = NormalizeKey(key),
            Value = NormalizeValue(value),
            DataType = NormalizeDataType(dataType),
        };
        row.SetCreated(userId);
        return row;
    }

    public void UpdateValue(string value, string dataType, Guid userId)
    {
        Value = NormalizeValue(value);
        DataType = NormalizeDataType(dataType);
        SetUpdated(userId);
    }

    private static string NormalizeScope(string scope) =>
        (scope ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeKey(string key) =>
        (key ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeValue(string value) =>
        (value ?? string.Empty).Trim();

    private static string NormalizeDataType(string dataType) =>
        (dataType ?? "string").Trim().ToLowerInvariant();
}


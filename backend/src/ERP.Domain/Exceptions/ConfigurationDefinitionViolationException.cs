using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Exceptions;

/// <summary>
/// CONFIG-FOUNDATION-P1-03: rechaza una escritura a org_settings que no cumple
/// ConfigurationDefinitionCatalog — key desconocida, scope no permitido, DataType que no
/// coincide con la definición, o valor que no pasa el Validator de la definición. Hereda de
/// <see cref="ArgumentException"/> para que ExceptionMiddleware la mapee automáticamente a 400
/// Bad Request sin necesitar un caso nuevo en el switch — es, semánticamente, un argumento de
/// escritura inválido.
///
/// Nunca se lanza en lectura/resolución — el fallback ahí es responsabilidad de cada resolver
/// tipado (ver ConfigurationFallbackStrategy). Esta excepción es exclusiva del guardrail de
/// escritura: "fallback solo aplica en lectura, nunca en escritura inválida".
/// </summary>
public sealed class ConfigurationDefinitionViolationException : ArgumentException
{
    public string Code { get; }

    private ConfigurationDefinitionViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static ConfigurationDefinitionViolationException UnknownKey(string key) =>
        new(
            "configuration_key_unknown",
            $"La key de configuración '{key}' no está registrada en ConfigurationDefinitionCatalog. "
                + "Toda configuración persistida debe tener una definición previa — no se acepta una key libre."
        );

    public static ConfigurationDefinitionViolationException ScopeNotAllowed(
        string key,
        OrgScope scope
    ) =>
        new(
            "configuration_scope_not_allowed",
            $"La key '{key}' no permite el scope '{scope}'."
        );

    public static ConfigurationDefinitionViolationException DataTypeMismatch(
        string key,
        SettingDataType provided,
        SettingDataType expected
    ) =>
        new(
            "configuration_data_type_mismatch",
            $"La key '{key}' espera DataType '{expected}' según su definición, se recibió '{provided}'."
        );

    public static ConfigurationDefinitionViolationException InvalidValue(string key, string? value) =>
        new(
            "configuration_value_invalid",
            $"El valor configurado para '{key}' no es válido para su definición."
        );
}

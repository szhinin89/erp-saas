using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.Application.Modules.Employees.DTOs;

/// <summary>
/// Campo de PATCH que distingue «omitido» (<see cref="IsSet"/> = false) de «enviado, posiblemente
/// null» (D-3). Un cuerpo sin la propiedad deja el valor por defecto.
/// </summary>
[JsonConverter(typeof(OptionalFieldConverterFactory))]
public readonly record struct OptionalField<T>(bool IsSet, T? Value)
{
    public static OptionalField<T> Of(T? value) => new(true, value);
}

public sealed class OptionalFieldConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(OptionalField<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)
            Activator.CreateInstance(
                typeof(OptionalFieldConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0])
            )!;

    private sealed class OptionalFieldConverter<T> : JsonConverter<OptionalField<T>>
    {
        // Solo se invoca cuando la propiedad está presente en el JSON (también con null).
        public override OptionalField<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        ) =>
            reader.TokenType == JsonTokenType.Null
                ? new OptionalField<T>(true, default)
                : new OptionalField<T>(true, JsonSerializer.Deserialize<T>(ref reader, options));

        // Un null expl�cito debe llegar a Read (si no, System.Text.Json devolver�a default = omitido).
        public override bool HandleNull => true;

        public override void Write(Utf8JsonWriter writer, OptionalField<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Value, options);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Domain.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ERP.API.Temporal;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — contrato de instante en el borde HTTP. Todo
/// <see cref="DateTime"/> que entra a la API (body JSON o query/route) es un instante y DEBE traer
/// zona explícita ("Z" u offset); sin zona se rechaza con 400 en vez de adivinar (el binder por
/// defecto de ASP.NET y System.Text.Json lo dejan Kind=Unspecified y el dominio terminaba
/// etiquetándolo UTC → drift ±5h). Una fecha de negocio viaja como <see cref="DateOnly"/>
/// ("YYYY-MM-DD") y no pasa por aquí. Parser único: <see cref="UtcDateTime.TryParseInstant"/>.
/// </summary>
public sealed class UtcInstantJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var text = reader.GetString();
        if (UtcDateTime.TryParseInstant(text, out var utc))
            return utc;
        throw new JsonException(
            $"Instante '{text}' sin zona horaria: enviar ISO-8601 UTC terminado en 'Z'."
        );
    }

    /// <summary>
    /// Salida: un instante se serializa siempre en UTC con sufijo "Z". Un valor Kind=Unspecified en
    /// una respuesta es un defecto del origen (debería ser DateOnly o UTC) — se reporta en vez de
    /// emitirlo sin zona.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcDateTime.EnsureUtc(value));
}

/// <summary>Mismo contrato que <see cref="UtcInstantJsonConverter"/> para query string / route.</summary>
public sealed class UtcInstantModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var text = value.FirstValue;
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        if (UtcDateTime.TryParseInstant(text, out var utc))
        {
            bindingContext.Result = ModelBindingResult.Success(utc);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"'{bindingContext.ModelName}' debe ser un instante ISO-8601 UTC terminado en 'Z'."
        );
        return Task.CompletedTask;
    }
}

public sealed class UtcInstantModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(DateTime)
            ? new UtcInstantModelBinder()
            : null;
}

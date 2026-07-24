namespace ERP.Domain.Common;

/// <summary>
/// Único punto de normalización para códigos de catálogo opcionales (tributarios: ICE, IVA
/// opcional de compra/venta, y cualquier código de catálogo que el dominio modele como
/// <c>string?</c> con NULL = "sin valor"). NULL/""/"   " colapsan siempre a NULL — nunca se
/// persiste string vacío como "sin valor" — y un código con contenido real se recorta.
/// </summary>
/// <remarks>
/// Blindaje de dominio: un &lt;select&gt; HTML no puede representar NULL (solo la opción "No
/// aplica" con <c>value=""</c>). Sin esta normalización en el punto de construcción/mutación del
/// dominio, "" viaja intacto hasta la base de datos y las validaciones downstream (que solo
/// comprueban <c>is not null</c>) lo tratan como un código real, fallando la búsqueda en catálogo.
/// </remarks>
public static class OptionalCode
{
    public static string? Normalize(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim();
}

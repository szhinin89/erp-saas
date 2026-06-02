namespace ERP.Application.Common;

/// <summary>
/// Helpers para NoteType — campo string en SalesNote/PurchNote.
/// UNA sola definición en Application para uso en handlers y event handlers.
/// La conversión NoteType → SRI doc code ("04"/"05") vive en los handlers de creación.
/// </summary>
public static class NoteTypeHelper
{
    /// <summary>
    /// Devuelve true si el NoteType corresponde a una nota de crédito.
    /// Acepta "CREDIT" (canónico) y "CREDITO" (valores legados en BD).
    /// Consumidores: EnviarVentasNotaSriCommandHandler, AprobarCompraNotaProveedorCommandHandler,
    ///               CompraNotaProveedorAprobadaEventHandler.
    /// </summary>
    public static bool IsCredit(string noteType) =>
        noteType.Trim().Equals("CREDIT",  StringComparison.OrdinalIgnoreCase) ||
        noteType.Trim().Equals("CREDITO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Código SRI de tipo de comprobante derivado del NoteType.
    /// "04" = nota de crédito, "05" = nota de débito.
    /// Consumidor: CrearVentasNotaCreditoDebitoCommandHandler (acceso a secuencial y clave de acceso).
    /// </summary>
    public static string ToSriDocCode(string noteType) =>
        IsCredit(noteType) ? "04" : "05";
}

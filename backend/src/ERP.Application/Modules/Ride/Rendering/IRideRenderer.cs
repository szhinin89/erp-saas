namespace ERP.Application.Modules.Ride.Rendering;

/// <summary>
/// Única abstracción del motor de render (ADR-025 §13). QuestPDF es únicamente una
/// implementación (llega en la Fase 7 del plan) — Application, Domain y los contratos públicos
/// nunca conocen QuestPDF; reemplazar el motor no afecta a esta interfaz.
/// </summary>
public interface IRideRenderer
{
    Task<byte[]> RenderAsync(IRideDocumentLayout layout, CancellationToken ct = default);
}

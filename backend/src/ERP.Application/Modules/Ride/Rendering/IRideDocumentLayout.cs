namespace ERP.Application.Modules.Ride.Rendering;

/// <summary>
/// Marcador opaco que produce <c>IRideTemplate.Compose(...)</c> y consume
/// <c>IRideRenderer.RenderAsync(...)</c> (ADR-025 §6/§13). Application nunca inspecciona su
/// contenido — cada implementación de <c>IRideRenderer</c> (QuestPDF u otro motor futuro) define
/// su propia forma concreta de layout sin que este contrato cambie.
/// </summary>
public interface IRideDocumentLayout;

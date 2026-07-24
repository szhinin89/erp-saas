using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Único punto donde vive el estilo de borde de caja del RIDE (1pt, negro) — antes repetido
/// como <c>.Border(1).BorderColor(Colors.Black)</c> literal en 13 sitios distintos entre las 7
/// Sections (auditoría de cierre). Cambiar el estilo de borde del formato oficial requiere editar
/// un único lugar en vez de 13.
/// </summary>
internal static class RideBoxBorderExtensions
{
    public static IContainer RideBox(this IContainer container) =>
        container.Border(1).BorderColor(Colors.Black);
}

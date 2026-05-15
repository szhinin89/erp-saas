namespace ERP.Application.Navigation.DTOs;

public sealed class AppFeatureArbolDto
{
    public Guid Id { get; init; }
    public string  Name { get; init; } = "";
    public string? Icono { get; init; }
    public string? Ruta { get; init; }
    public string Permiso { get; init; } = "";
    public IReadOnlyList<AppFeatureArbolDto> Hijos { get; init; } = Array.Empty<AppFeatureArbolDto>();
}

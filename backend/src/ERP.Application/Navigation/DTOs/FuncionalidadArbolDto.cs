namespace ERP.Application.Navigation.DTOs;

public sealed class FuncionalidadArbolDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Icono { get; init; }
    public string? Ruta { get; init; }
    public string Permiso { get; init; } = "";
    public IReadOnlyList<FuncionalidadArbolDto> Hijos { get; init; } = Array.Empty<FuncionalidadArbolDto>();
}

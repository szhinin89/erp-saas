namespace ERP.Application.Navigation.DTOs;

public sealed class AppFeatureTreeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Icon { get; init; }
    public string? Path { get; init; }
    public string Permission { get; init; } = "";
    public IReadOnlyList<AppFeatureTreeDto> Children { get; init; } = Array.Empty<AppFeatureTreeDto>();
}

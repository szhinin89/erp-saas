using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using ERP.Domain.Modules.Menu.Interfaces;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetAppFeatureTree;

public sealed record GetAppFeatureTreeQuery : IRequest<Result<IReadOnlyList<AppFeatureTreeDto>>>;

public sealed class GetAppFeatureTreeQueryHandler
    : IRequestHandler<GetAppFeatureTreeQuery, Result<IReadOnlyList<AppFeatureTreeDto>>>
{
    private readonly IAppFeatureRepository _repository;

    public GetAppFeatureTreeQueryHandler(IAppFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<AppFeatureTreeDto>>> Handle(
        GetAppFeatureTreeQuery request,
        CancellationToken ct)
    {
        var rows = await _repository.ListVisibleMenuRowsAsync(ct);

        List<AppFeatureTreeDto> BuildTree(Guid? parentId)
        {
            return rows
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => FunctionalModuleRank(ExtractFunctionalModuleKey(x.Path, x.Permission, x.Name)))
                .ThenBy(x => ExtractFunctionalModuleKey(x.Path, x.Permission, x.Name))
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new AppFeatureTreeDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Icon = x.Icon,
                    Path = x.Path,
                    Permission = x.Permission,
                    Children = BuildTree(x.Id),
                })
                .ToList();
        }

        return Result<IReadOnlyList<AppFeatureTreeDto>>.Success(BuildTree(null));
    }

    private static string ExtractFunctionalModuleKey(string? path, string? permission, string? name)
    {
        var route = (path ?? string.Empty).Trim().ToLowerInvariant();
        if (route.StartsWith('/'))
        {
            var first = route.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        var perm = (permission ?? string.Empty).Trim().ToLowerInvariant();
        if (perm.StartsWith("perm:"))
            perm = perm["perm:".Length..];
        var permModule = perm.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(permModule))
            return permModule;

        return (name ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static int FunctionalModuleRank(string moduleKey) => moduleKey switch
    {
        "inventario" => 10,
        "ventas" => 20,
        "compras" => 30,
        "caja" => 40,
        "contabilidad" => 50,
        "gastos" => 60,
        "products" => 70,
        "productos" => 70,
        "access" => 80,
        "security" => 90,
        _ => 500,
    };
}

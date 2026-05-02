using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed class GetSessionMenuHandler
{
    private readonly INavigationMenuReader _reader;

    public GetSessionMenuHandler(INavigationMenuReader reader) => _reader = reader;

    public async Task<Result<IReadOnlyList<SessionMenuGroupDto>>> HandleAsync(CancellationToken ct = default)
    {
        var menu = await _reader.GetActiveMenuAsync(ct);
        return Result<IReadOnlyList<SessionMenuGroupDto>>.Success(menu);
    }
}

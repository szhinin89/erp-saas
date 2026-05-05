using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed class GetSessionMenuHandler : IRequestHandler<GetSessionMenuQuery, Result<IReadOnlyList<SessionMenuGroupDto>>>
{
    private readonly INavigationMenuReader _reader;

    public GetSessionMenuHandler(INavigationMenuReader reader) => _reader = reader;

    public Task<Result<IReadOnlyList<SessionMenuGroupDto>>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetSessionMenuQuery(), ct);

    public async Task<Result<IReadOnlyList<SessionMenuGroupDto>>> Handle(GetSessionMenuQuery request, CancellationToken ct)
    {
        var menu = await _reader.GetActiveMenuAsync(ct);
        return Result<IReadOnlyList<SessionMenuGroupDto>>.Success(menu);
    }
}

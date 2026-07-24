using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using MediatR;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed class GetSessionMenuHandler
    : IRequestHandler<GetSessionMenuQuery, Result<IReadOnlyList<NavMenuGroupDto>>>
{
    private readonly INavigationBuilder _builder;

    public GetSessionMenuHandler(INavigationBuilder builder) => _builder = builder;

    public async Task<Result<IReadOnlyList<NavMenuGroupDto>>> Handle(
        GetSessionMenuQuery request, CancellationToken cancellationToken)
    {
        var groups = await _builder.BuildMenuAsync(cancellationToken);
        return Result<IReadOnlyList<NavMenuGroupDto>>.Success(groups);
    }
}

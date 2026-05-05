using MediatR;
using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation.UseCases.GetSessionMenu;

public sealed record GetSessionMenuQuery : IRequest<Result<IReadOnlyList<SessionMenuGroupDto>>>;

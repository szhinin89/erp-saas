using MediatR;
using ERP.Application.Common;
using ERP.Application.Security.DTOs;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public record GetSecurityAdminMatrixQuery : IRequest<Result<(IReadOnlyList<SecurityUserDto> Users, IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments)>>;

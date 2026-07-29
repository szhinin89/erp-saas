using ERP.Application.Common;
using ERP.Application.Security.DTOs;
using MediatR;

namespace ERP.Application.Security.UseCases.GetSecurityAdminMatrix;

public record GetSecurityAdminMatrixQuery
    : IRequest<
        Result<(
            IReadOnlyList<SecurityUserDto> Users,
            IReadOnlyList<SecurityAdminScopeAssignmentDto> Assignments
        )>
    >;

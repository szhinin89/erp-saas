using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.OperateCompany;

/// <summary>
/// AdminGlobalCore: un admin global (tenant_id == Guid.Empty) elige una empresa concreta para
/// operar. Emite un token operativo scoped al tenant/empresa/sucursal real — el admin global
/// nunca usa su token global directo contra endpoints operativos. Ver <see cref="OperateCompanyHandler"/>.
/// </summary>
public sealed record OperateCompanyCommand(Guid CompanyId) : IRequest<Result<AuthResponseDto>>;

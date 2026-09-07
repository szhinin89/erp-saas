using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SetSupplierRetentionDefaultState;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — activa/desactiva (soft-disable, nunca DELETE físico)
/// y/o reordena una retención predeterminada existente. Company-scoped: la fila debe pertenecer a
/// la empresa activa del JWT (el query filter global ya lo garantiza fail-closed).
/// </summary>
public sealed record SetSupplierRetentionDefaultStateCommand(
    Guid Id,
    bool IsActive,
    int DisplayOrder
) : IRequest<Result<SupplierRetentionDefaultDto>>, ICompanyScopedRequest;

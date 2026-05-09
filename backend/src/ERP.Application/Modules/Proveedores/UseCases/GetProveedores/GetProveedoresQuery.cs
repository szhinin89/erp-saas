using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Proveedores.DTOs;

namespace ERP.Application.Modules.Proveedores.UseCases.GetProveedores;

public sealed record GetProveedoresQuery(
    bool?   ActiveFilter,
    string? Search,
    string? TipoPersona
) : IRequest<Result<IReadOnlyList<ProveedorDto>>>;
